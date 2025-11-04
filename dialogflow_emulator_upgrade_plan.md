# План модернизации эмулятора Dialogflow для поддержки gRPC

Этот документ описывает шаги и возможные решения для перехода от HTTP-эмулятора клиента к полноценному gRPC-эмулятору сервиса Dialogflow.

## 1. Проблема

Текущая реализация использует кастомный `DialogflowEmulatorClient`, который отправляет HTTP-запросы на Node.js эмулятор. Это имеет несколько недостатков:

- **Неполное тестирование**: Локальная отладка не использует нативную библиотеку `Google.Cloud.Dialogflow.V2`, что может скрывать проблемы, связанные с gRPC, аутентификацией и обработкой ошибок в реальной среде.
- **Избыточный код**: Требуется поддерживать отдельный клиент (`DialogflowEmulatorClient`) и логику преобразования данных между gRPC-моделями и JSON.
- **Ограниченные возможности**: Эмулятор может не поддерживать все функции официального API, доступные через gRPC (например, потоковую передачу аудио).

## 2. Цель

Заменить текущий HTTP-эмулятор на сервис, совместимый с **gRPC**. Это позволит использовать стандартный `SessionsClient` из библиотеки `Google.Cloud.Dialogflow.V2` для локальной отладки, просто указав адрес локального эмулятора.

## 3. Ключевые выводы исследования

1.  **Протокол**: Библиотека `Google.Cloud.Dialogflow.V2` использует **gRPC** для взаимодействия с API.
2.  **Смена эндпоинта**: Библиотека позволяет указать кастомный адрес сервиса через класс `SessionsClientBuilder` и его свойство `Endpoint`.
3.  **Готовые эмуляторы**: Поиск не выявил готовых open-source gRPC-эмуляторов для Dialogflow. Решение придется создавать самостоятельно.

## 4. Возможные решения

### Решение A: Создание gRPC-обертки над существующим HTTP-эмулятором

Создать новый сервис (например, на Node.js или .NET), который будет принимать gRPC-запросы, преобразовывать их в HTTP-запросы к вашему текущему эмулятору, а затем возвращать ответ в формате gRPC.

-   **Плюсы**:
    -   Быстрое внедрение, так как основная логика эмуляции уже реализована.
    -   Не требует глубокого понимания механики работы Dialogflow.
-   **Минусы**:
    -   Добавляет еще один слой абстракции, усложняя отладку.
    -   Потенциальное снижение производительности из-за двойного преобразования.
    -   Сохраняет зависимость от старого HTTP-эмулятора.

### Решение B: Переписывание эмулятора на .NET с использованием gRPC (Рекомендуемое)

Реализовать логику вашего Node.js эмулятора (чтение файлов агента, сопоставление интентов) с нуля в виде нового gRPC-сервиса на .NET.

-   **Плюсы**:
    -   Единый технологический стек с основным приложением.
    -   Высокая производительность и отсутствие лишних преобразований.
    -   Полный контроль над реализацией и возможность расширения.
    -   Более простое и чистое решение в долгосрочной перспективе.
-   **Минусы**:
    -   Требует больше времени на первоначальную разработку.

## 5. Пошаговый план (для Решения B)

### Шаг 1: Подготовка проекта

1.  Создайте новый проект в вашем решении: **ASP.NET Core gRPC Service** (например, `FillInTheTextBot.Dialogflow.Emulator`).
2.  Добавьте в него ссылку на `.proto` файлы Dialogflow. Самый простой способ — добавить пакеты NuGet, которые их содержат:
    ```xml
    <ItemGroup>
        <PackageReference Include="Google.Api.CommonProtos" Version="2.15.0" />
        <PackageReference Include="Google.Cloud.Dialogflow.V2" Version="4.10.0" />
        <PackageReference Include="Grpc.AspNetCore" />
    </ItemGroup>
    ```

### Шаг 2: Реализация gRPC-сервиса

1.  Создайте класс сервиса, который наследуется от `Sessions.SessionsBase` (сгенерированный из `.proto` файла).
    ```csharp
    public class DialogflowEmulatorService : Sessions.SessionsBase
    {
        private readonly ILogger<DialogflowEmulatorService> _logger;

        public DialogflowEmulatorService(ILogger<DialogflowEmulatorService> logger)
        {
            _logger = logger;
        }

        public override Task<DetectIntentResponse> DetectIntent(DetectIntentRequest request, ServerCallContext context)
        {
            // Здесь будет логика эмуляции
            _logger.LogInformation("DetectIntent request for session: {Session}", request.Session);

            // TODO: Реализовать логику поиска интента

            var response = new DetectIntentResponse
            {
                // ... заполнить ответ
            };

            return Task.FromResult(response);
        }
    }
    ```
2.  Перенесите логику чтения файлов агента (`agent.json`, `intents/*.json`) из Node.js эмулятора в новый .NET-сервис.
3.  Реализуйте базовый алгоритм сопоставления текста запроса с интентами.

### Шаг 3: Интеграция с основным приложением

1.  В файле `appsettings.Local.json` измените `EmulatorEndpoint`, указав порт вашего нового gRPC-сервиса (например, `localhost:5001`).
2.  Измените код, отвечающий за создание клиента `SessionsClient`. Вместо `DialogflowEmulatorClient` используйте `SessionsClientBuilder`:

    ```csharp
    // Фрагмент кода для ExternalServicesRegistration.cs или аналогичного

    if (!string.IsNullOrEmpty(config.EmulatorEndpoint))
    {
        // Используем gRPC-эмулятор
        var sessionsClientBuilder = new SessionsClientBuilder
        {
            Endpoint = config.EmulatorEndpoint,
            ChannelCredentials = Grpc.Core.ChannelCredentials.Insecure // Для локальной отладки без TLS
        };
        
        services.AddSingleton(await sessionsClientBuilder.BuildAsync());
    }
    else
    {
        // Используем реальный Dialogflow
        var sessionsClientBuilder = new SessionsClientBuilder
        {
            CredentialsPath = config.JsonPath
        };

        services.AddSingleton(await sessionsClientBuilder.BuildAsync());
    }
    ```

3.  Удалите старый `DialogflowEmulatorClient` и связанные с ним классы-модели.

### Шаг 4: Настройка Docker Compose

1.  Создайте `Dockerfile` для нового gRPC-эмулятора.
2.  Обновите `docker-compose.yml`, чтобы он собирал и запускал .NET-эмулятор вместо Node.js-версии.

## 6. Следующие шаги

Я готов приступить к реализации **Решения B**. Если вы согласны с этим планом, я начну с создания нового проекта gRPC-сервиса в вашем решении.
