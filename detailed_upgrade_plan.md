# Детальный план миграции эмулятора Dialogflow на .NET gRPC

Этот документ подробно описывает шаги по реализации **Решения B** из первоначального плана — полного переноса логики Node.js эмулятора на .NET с использованием gRPC.

## Шаг 1: Подготовка .NET проекта

1.  **Создание проекта**:
    *   Создайте новый проект типа **ASP.NET Core gRPC Service**, целевой фреймворк net9.0.
    *   Название проекта: `Dialogflow.Emulator`.
    *   Поместите его в папку `src` вашего решения.

2.  **Добавление зависимостей**:
    *   Добавьте следующие пакеты свевжих версий. Они обеспечат поддержку gRPC и предоставят сгенерированные классы для работы с Dialogflow API.
    *   Также укажите свежие верси этих пакетов в Directory.Packages.props

    ```xml
    <ItemGroup>
      <PackageReference Include="Google.Cloud.Dialogflow.V2" />
      <PackageReference Include="Grpc.AspNetCore" />
    </ItemGroup>
    ```

3.  **Настройка запуска**:
    *   В файле `Properties/launchSettings.json` убедитесь, что порт для HTTPS (`applicationUrl`) установлен (например, `https://localhost:2511`) и запомните его. Этот порт будет использоваться для `EmulatorEndpoint`.

## Шаг 2: Перенос логики чтения файлов агента

Эта часть заменит функцию `loadAgentData` из `server.js`.

1.  **Создание моделей (DTO)**:
    *   Создайте папку `Models`.
    *   В ней создайте C# `record`-ы, повторяющие структуру JSON-файлов интентов. Это позволит использовать современный и лаконичный синтаксис.

    ```csharp
    // Models/Intent.cs
    namespace FillInTheTextBot.Dialogflow.Emulator.Models;

    using System.Text.Json.Serialization;

    public record Intent(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("responses")] IReadOnlyList<IntentResponse> Responses,
        [property: JsonPropertyName("events")] IReadOnlyList<IntentEvent> Events
    );

    public record IntentResponse(
        [property: JsonPropertyName("messages")] IReadOnlyList<ResponseMessage> Messages
    );

    public record ResponseMessage(
        [property: JsonPropertyName("speech")] IReadOnlyList<string> Speech
    );

    public record IntentEvent(
        [property: JsonPropertyName("name")] string Name
    );
    ```

2.  **Создание сервиса для загрузки данных**:
    *   Создайте интерфейс `IAgentStorage` и его реализацию `AgentStorage`.
    *   Этот сервис будет отвечать за чтение и хранение всех интентов в памяти.

    ```csharp
    // Services/IAgentStorage.cs
    using FillInTheTextBot.Dialogflow.Emulator.Models;

    public interface IAgentStorage
    {
        Task InitializeAsync(string agentPath);
        Intent GetIntent(string name);
        Intent FindIntentByEvent(string eventName);
        IEnumerable<Intent> GetAllIntents();
    }

    // Services/AgentStorage.cs
    public class AgentStorage : IAgentStorage
    {
        private readonly ILogger<AgentStorage> _logger;
        private Dictionary<string, Intent> _intents = new();

        public AgentStorage(ILogger<AgentStorage> logger) => _logger = logger;

        public async Task InitializeAsync(string agentPath)
        {
            var intentsPath = Path.Combine(agentPath, "intents");
            if (!Directory.Exists(intentsPath)) 
            {
                _logger.LogWarning("Intents directory not found at {Path}", intentsPath);
                return;
            }

            var intentFiles = Directory.GetFiles(intentsPath, "*.json")
                                       .Where(file => !file.Contains("_usersays_"));

            foreach (var file in intentFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var intent = JsonSerializer.Deserialize<Intent>(json);
                    if (intent != null && !string.IsNullOrEmpty(intent.Name))
                    {
                        _intents[intent.Name] = intent;
                        _logger.LogInformation("Loaded intent: {IntentName}", intent.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load intent from {File}", file);
                }
            }
            _logger.LogInformation("Total intents loaded: {Count}", _intents.Count);
        }

        public Intent GetIntent(string name) => _intents.GetValueOrDefault(name);

        public Intent FindIntentByEvent(string eventName) => 
            _intents.Values.FirstOrDefault(i => i.Events?.Any(e => e.Name == eventName) ?? false);

        public IEnumerable<Intent> GetAllIntents() => _intents.Values;
    }
    ```

3.  **Регистрация и инициализация**:
    *   В `Program.cs` зарегистрируйте `AgentStorage` как Singleton и вызовите его инициализацию при старте приложения.

    ```csharp
    // Program.cs (фрагмент)
    var builder = WebApplication.CreateBuilder(args);

    // ... другие сервисы
    builder.Services.AddSingleton<IAgentStorage, AgentStorage>();

    var app = builder.Build();

    // Инициализация хранилища интентов
    var agentStorage = app.Services.GetRequiredService<IAgentStorage>();
    var agentPath = builder.Configuration.GetValue<string>("AGENT_PATH") ?? "/app/agent";
    await agentStorage.InitializeAsync(agentPath);

    // ... настройка пайплайна
    ```

## Шаг 3: Реализация алгоритма сопоставления

Этот сервис заменит `findIntentByText` и `getFallbackIntent`.

1.  **Создание сервиса `IntentMatcher`**:

    ```csharp
    // Services/IIntentMatcher.cs
    public interface IIntentMatcher
    {
        Intent Match(string text);
    }

    // Services/IntentMatcher.cs
    public class IntentMatcher : IIntentMatcher
    {
        private readonly IAgentStorage _agentStorage;
        private readonly Dictionary<string, string[]> _keywordMap;

        public IntentMatcher(IAgentStorage agentStorage)
        {
            _agentStorage = agentStorage;
            // Эта карта должна быть идентична той, что в server.js
            _keywordMap = new Dictionary<string, string[]> 
            {
                { "EasyWelcome", ["да", "конечно", "давай"] },
                { "Exit", ["выход", "выйти", "стоп", "пока"] },
                // ... и так далее для всех интентов
            };
        }

        public Intent Match(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return GetFallbackIntent();

            var lowerText = text.ToLowerInvariant().Trim();

            foreach (var (intentName, keywords) in _keywordMap)
            {
                if (keywords.Any(keyword => lowerText.Contains(keyword)))
                {
                    var intent = _agentStorage.GetIntent(intentName);
                    if (intent != null) return intent;
                }
            }

            return GetFallbackIntent();
        }

        private Intent GetFallbackIntent() => _agentStorage.GetIntent("Default Fallback Intent");
    }
    ```

2.  **Регистрация в DI**:
    *   В `Program.cs` добавьте:
    `builder.Services.AddScoped<IIntentMatcher, IntentMatcher>();`

## Шаг 4: Реализация gRPC-сервиса

Это ядро эмулятора, которое будет обрабатывать gRPC-вызовы.

1.  **Создание `DialogflowEmulatorService`**:
    *   Создайте класс в папке `Services`, который наследуется от `Sessions.SessionsBase`.

    ```csharp
    // Services/DialogflowEmulatorService.cs
    using Google.Cloud.Dialogflow.V2;
    using Grpc.Core;
    using static Google.Cloud.Dialogflow.V2.Sessions;

    public class DialogflowEmulatorService : SessionsBase
    {
        private readonly ILogger<DialogflowEmulatorService> _logger;
        private readonly IAgentStorage _agentStorage;
        private readonly IIntentMatcher _intentMatcher;

        public DialogflowEmulatorService(ILogger<DialogflowEmulatorService> logger, IAgentStorage agentStorage, IIntentMatcher intentMatcher)
        {
            _logger = logger;
            _agentStorage = agentStorage;
            _intentMatcher = intentMatcher;
        }

        public override Task<DetectIntentResponse> DetectIntent(DetectIntentRequest request, ServerCallContext context)
        {
            _logger.LogInformation("DetectIntent request for session: {Session}", request.Session);

            Models.Intent matchedIntent = null;
            var queryText = "";

            if (request.QueryInput.Event != null)
            {
                queryText = $"event:{request.QueryInput.Event.Name}";
                matchedIntent = _agentStorage.FindIntentByEvent(request.QueryInput.Event.Name);
            }
            else if (request.QueryInput.Text != null)
            {
                queryText = request.QueryInput.Text.Text;
                matchedIntent = _intentMatcher.Match(queryText);
            }

            matchedIntent ??= _agentStorage.GetIntent("Default Fallback Intent");

            var response = CreateDetectIntentResponse(matchedIntent, queryText, request.Session);
            return Task.FromResult(response);
        }

        private DetectIntentResponse CreateDetectIntentResponse(Models.Intent intent, string queryText, string sessionId)
        {
            var fulfillmentText = "Ответ не найден.";
            if (intent?.Responses.FirstOrDefault()?.Messages.FirstOrDefault()?.Speech.FirstOrDefault() is { } speech)
            {
                fulfillmentText = speech;
            }

            var queryResult = new QueryResult
            {
                QueryText = queryText,
                FulfillmentText = fulfillmentText,
                Intent = new Intent
                {
                    DisplayName = intent?.Name ?? "Default Fallback Intent",
                    Name = $"{sessionId}/intents/{intent?.Id ?? Guid.NewGuid().ToString()}"
                },
                IntentDetectionConfidence = 0.85f, // Эмуляция
                LanguageCode = "ru"
            };
            queryResult.FulfillmentMessages.Add(new FulfillmentMessage { Text = new Text { Text_ = { fulfillmentText } } });

            return new DetectIntentResponse
            {
                ResponseId = Guid.NewGuid().ToString(),
                QueryResult = queryResult
            };
        }
    }
    ```

2.  **Регистрация эндпоинта**:
    *   В `Program.cs` добавьте:
    `app.MapGrpcService<DialogflowEmulatorService>();`

## Шаг 5: Интеграция с основным приложением

1.  **Обновление `appsettings.Local.json`**:
    *   Найдите или добавьте секцию `Dialogflow` и укажите `EmulatorEndpoint`, используя порт из `launchSettings.json`.

    ```json
    "Dialogflow": {
      "EmulatorEndpoint": "localhost:7195"
    }
    ```

2.  **Обновление `ExternalServicesRegistration.cs`**:
    *   Замените логику создания клиента, как было предложено в `dialogflow_emulator_upgrade_plan.md`. Это позволит прозрачно переключаться между реальным API и эмулятором.

3.  **Удаление старого кода**:
    *   После проверки работоспособности нового эмулятора, удалите `DialogflowEmulatorClient` и все связанные с ним модели из основного проекта.

## Шаг 6: Настройка Docker Compose

1.  **Создание `Dockerfile`**:
    *   В корне проекта `FillInTheTextBot.Dialogflow.Emulator` создайте `Dockerfile`.

    ```dockerfile
    FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
    WORKDIR /src
    COPY ["src/FillInTheTextBot.Dialogflow.Emulator/FillInTheTextBot.Dialogflow.Emulator.csproj", "FillInTheTextBot.Dialogflow.Emulator/"]
    # Копирование остальных .csproj и восстановление зависимостей
    # ... (нужно адаптировать под вашу структуру)
    RUN dotnet restore "FillInTheTextBot.Dialogflow.Emulator/FillInTheTextBot.Dialogflow.Emulator.csproj"
    
    COPY . .
    WORKDIR "/src/FillInTheTextBot.Dialogflow.Emulator"
    RUN dotnet build "FillInTheTextBot.Dialogflow.Emulator.csproj" -c Release -o /app/build

    FROM build AS publish
    RUN dotnet publish "FillInTheTextBot.Dialogflow.Emulator.csproj" -c Release -o /app/publish

    FROM mcr.microsoft.com/dotnet/aspnet:8.0
    WORKDIR /app
    COPY --from=publish /app/publish .
    ENTRYPOINT ["dotnet", "FillInTheTextBot.Dialogflow.Emulator.dll"]
    ```

2.  **Обновление `docker-compose.yml`**:
    *   Закомментируйте или удалите сервис `dialogflow-emulator` (Node.js).
    *   Добавьте новый сервис для .NET-эмулятора.

    ```yaml
    services:
      # ... другие сервисы

      dialogflow-emulator-grpc:
        container_name: dialogflow-emulator-grpc
        build:
          context: .
          dockerfile: src/FillInTheTextBot.Dialogflow.Emulator/Dockerfile
        ports:
          - "7195:8080" # Маппинг порта gRPC
        environment:
          - AGENT_PATH=/app/agent
        volumes:
          - ./dialogflow-emulator:/app/agent # Важно: монтируем ту же папку с интентами
    ```

## Шаг 7: Тест
Напишите тест, который запускает сервис, подключает к нему папку Dialogflow\FillInTheTextBot-test-eu, и выполняет просто запрос (например, чтобы сработал intent Welcome). Проект теста должен быть написан под nUnit