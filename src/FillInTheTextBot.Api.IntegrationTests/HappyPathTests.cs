using System.Net;
using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FillInTheTextBot.Messengers.Yandex;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FillInTheTextBot.Api.IntegrationTests;

/// <summary>
/// Сквозной прогон: настоящий HTTP-запрос в приложение, настоящий Redis в контейнере
/// и эмулятор Dialogflow вместо облака.
/// </summary>
[TestFixture]
public class HappyPathTests
{
    private const string AgentDirectory = "FillInTheTextBot-test-eu";
    private const int RedisPort = 6379;

    private EmulatorFixture _emulator = null!;
    private IContainer _redis = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _yandexPath = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var agentPath = Path.Combine(GetSolutionRoot(), "Dialogflow", AgentDirectory);

        _emulator = new EmulatorFixture();
        await _emulator.StartAsync(agentPath);

        _redis = new ContainerBuilder("redis:alpine")
            .WithPortBinding(RedisPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
            .Build();

        await _redis.StartAsync();

        var redisConnectionString = $"localhost:{_redis.GetMappedPublicPort(RedisPort)}";

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AppConfiguration:Dialogflow:0:ScopeId", "emulator");
            builder.UseSetting("AppConfiguration:Dialogflow:0:ProjectId", "fillinthetextbot-test");
            builder.UseSetting("AppConfiguration:Dialogflow:0:EmulatorEndpoint", _emulator.Endpoint);
            builder.UseSetting("AppConfiguration:Redis:ConnectionString", redisConnectionString);
            builder.UseSetting("AppConfiguration:Tracing:Port", "0");

            builder.UseSetting("Logging:LogLevel:Default", "Warning");
        });

        _client = _factory.CreateClient();

        // Контроллер отдаёт 404, если токен в запросе не совпадает с настроенным,
        // поэтому берём его из той же конфигурации, что и приложение
        var incomingToken = _factory.Services.GetRequiredService<YandexConfiguration>().IncomingToken;

        _yandexPath = string.IsNullOrEmpty(incomingToken)
            ? "/yandex"
            : $"/yandex/{Uri.EscapeDataString(incomingToken)}";
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _client?.Dispose();

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_redis is not null)
        {
            await _redis.DisposeAsync();
        }

        if (_emulator is not null)
        {
            await _emulator.DisposeAsync();
        }
    }

    [Test]
    public async Task NewSession_WelcomeIntentAnswer()
    {
        var body = await PostYandexAsync(CreatePayload(isNewSession: true, command: string.Empty));

        Assert.That(GetResponseText(body), Does.StartWith("Добро пожаловать!"),
            "Пустая команда в новой сессии должна уходить событием Welcome в Default Welcome Intent");
    }

    [Test]
    public async Task KnownTrainingPhrase_MatchedIntentAnswer()
    {
        var sessionId = Guid.NewGuid().ToString("N");

        await PostYandexAsync(CreatePayload(isNewSession: true, command: string.Empty, sessionId: sessionId));

        var body = await PostYandexAsync(CreatePayload(isNewSession: false, command: "помощь", sessionId: sessionId));

        Assert.That(GetResponseText(body), Does.StartWith("Чтобы сочинить историю"),
            "Обучающая фраза «помощь» принадлежит интенту Help");
    }

    [Test]
    public async Task UnknownPhrase_FallbackAnswer()
    {
        var body = await PostYandexAsync(CreatePayload(isNewSession: false, command: "кркркр"));

        Assert.That(GetResponseText(body), Does.StartWith("Не совсем понимаю"),
            "Неизвестная фраза должна уходить в Default Fallback Intent");
    }

    [Test]
    public async Task Always_MetricsEndpointExposesCustomMetric()
    {
        await PostYandexAsync(CreatePayload(isNewSession: true, command: string.Empty));

        var metrics = await _client.GetStringAsync("/metrics");

        Assert.That(metrics, Does.Contain("metrics{"),
            "Кастомная метрика должна отдаваться под прежним именем, иначе сломаются дашборды");
    }

    private async Task<string> PostYandexAsync(object payload)
    {
        var response = await _client.PostAsync(_yandexPath, JsonContent.Create(payload));

        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);

        return body;
    }

    private static string? GetResponseText(string body)
    {
        using var document = System.Text.Json.JsonDocument.Parse(body);

        var text = document.RootElement.GetProperty("response").GetProperty("text").GetString();

        return text;
    }

    private static object CreatePayload(bool isNewSession, string command, string? sessionId = null)
    {
        return new
        {
            meta = new { locale = "ru-RU", timezone = "UTC", client_id = "integration-tests" },
            session = new
            {
                message_id = 0,
                session_id = sessionId ?? Guid.NewGuid().ToString("N"),
                skill_id = "integration-tests",
                user_id = Guid.NewGuid().ToString("N"),
                @new = isNewSession
            },
            request = new
            {
                command,
                original_utterance = command,
                type = "SimpleUtterance",
                markup = new { dangerous_context = false },
                nlu = new { tokens = Array.Empty<string>(), entities = Array.Empty<object>() }
            },
            state = new { session = new { }, user = new { } },
            version = "1.0"
        };
    }

    private static string GetSolutionRoot()
    {
        var directory = TestContext.CurrentContext.TestDirectory;

        while (directory is not null && !File.Exists(Path.Combine(directory, "src", "FillInTheTextBot.slnx")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Не найден корень репозитория");
        }

        return directory;
    }
}
