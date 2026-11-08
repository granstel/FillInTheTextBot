using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FillInTheTextBot.Messengers.Marusia;
using FillInTheTextBot.Messengers.Sber;
using FillInTheTextBot.Models;
using Google.Cloud.Dialogflow.V2;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Core.Interceptors;
using StackExchange.Redis;
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
        await _emulator.StartAsync(agentPath, options => options.Interceptors.Add<StoryPayloadInterceptor>());

        _redis = new ContainerBuilder(System.Environment.GetEnvironmentVariable("REDIS_TEST_IMAGE") ?? "redis:alpine")
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
            builder.UseSetting("AppConfiguration:Dialogflow:0:LogQuery", "true");
            builder.UseSetting("AppConfiguration:Redis:ConnectionString", redisConnectionString);
            builder.UseSetting("AppConfiguration:Redis:KeyPrefix", "integration-json:");
            builder.UseSetting("AppConfiguration:Tracing:Port", "0");

            // Пауза вывода из ротации нужна в бою, в тестах она только замедляет остановку
            builder.UseSetting("AppConfiguration:Shutdown:DrainDelaySeconds", "0");
            builder.UseSetting("AppConfiguration:Rotation:Enabled", "false");
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


    [TestCase("yandex")]
    [TestCase("marusia")]
    public async Task Webhook_PreservesWireNamesAndLegacyState(string platform)
    {
        const string legacyState = """{"Cycle":1,"Done":"AQ==","Current":29}""";
        var sessionId = Guid.NewGuid().ToString("N");
        var payload = CreatePayload(isNewSession: false, command: "ping", sessionId,
            userState: new Dictionary<string, object> { ["ISOLDUSER"] = true, ["NEXTTEXTINDEX"] = "7", ["ROTATIONSTATE"] = legacyState });
        var path = GetWebhookPath(platform);
        using var body = await PostAsync(path, payload);
        var root = body.RootElement;
        var answer = root.GetProperty("response");
        var session = root.GetProperty("session");
        var state = root.GetProperty("user_state_update");

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("version").GetString(), Is.EqualTo("1.0"));
            Assert.That(session.GetProperty("session_id").GetString(), Is.EqualTo(sessionId));
            Assert.That(session.GetProperty("skill_id").GetString(), Is.EqualTo("integration-tests"));
            Assert.That(session.GetProperty("new").GetBoolean(), Is.False);
            Assert.That(answer.GetProperty("text").GetString(), Is.EqualTo("pong"));
            Assert.That(answer.GetProperty("end_session").GetBoolean(), Is.False);
            Assert.That(answer.TryGetProperty("tts", out _), Is.False);
            Assert.That(answer.TryGetProperty("buttons", out _), Is.False);
            Assert.That(state.GetProperty("NEXTTEXTINDEX").GetInt32(), Is.EqualTo(7));
            Assert.That(state.GetProperty("ROTATIONSTATE").GetString(), Is.EqualTo(legacyState));
            Assert.That(state.GetProperty(Request.IsOldUserKey).GetBoolean(), Is.True);
            Assert.That(root.GetProperty("session_state").GetProperty("SCOPEKEY").ValueKind, Is.EqualTo(JsonValueKind.Null));
        });
        if (platform == "yandex")
        {
            Assert.That(root.GetProperty("application_state").GetProperty("NEXTTEXTINDEX").GetInt32(), Is.EqualTo(7));
        }
    }

    [Test]
    public async Task CompletedStory_MapsDialogflowPayloadToPlatformButton()
    {
        var sessionId = Guid.NewGuid().ToString("N");
        await PostYandexAsync(CreatePayload(true, "", sessionId));
        foreach (var word in new[] { "опоздала в больницу", "3", "медведь", "два", "зайцы", "лес" })
        {
            await PostYandexAsync(CreatePayload(false, word, sessionId));
        }

        using var body = await PostAsync(_yandexPath, CreatePayload(false, "5", sessionId));
        var response = body.RootElement.GetProperty("response");
        var buttons = response.GetProperty("buttons").EnumerateArray();

        Assert.That(response.GetProperty("text").GetString(), Does.Contain("Вот что получилось"));
        Assert.That(buttons.Any(button =>
            button.GetProperty("title").GetString() == "Кнопка из payload" &&
            button.GetProperty("url").GetString() == "https://example.test/story" &&
            !button.GetProperty("hide").GetBoolean()), Is.True);
    }

    [Test]
    public async Task SberWebhook_RoundTripsDeviceAndUpdatesLegacyRedisState()
    {
        var userId = Guid.NewGuid().ToString("N");
        var database = _factory.Services.GetRequiredService<IDatabase>();
        RedisKey cacheKey = $"integration-json:sber:{userId}";
        const string legacyState = """{"IsOldUser":true,"NextTextIndex":7,"RotationState":"{\"Cycle\":1,\"Done\":\"AQ==\"}"}""";
        await database.StringSetAsync(cacheKey, legacyState);
        var payload = new
        {
            messageName = "MESSAGE_TO_SKILL",
            messageId = "42",
            sessionId = "sber-session",
            uuid = new { sub = userId, userChannel = "test-channel" },
            payload = new
            {
                new_session = false,
                device = new { platformType = "test-platform", capabilities = new { screen = new { available = true } } },
                message = new { original_text = "помощь" }
            }
        };

        using var body = await PostAsync(GetWebhookPath("sber"), payload);
        var root = body.RootElement;
        var answer = root.GetProperty("payload");
        var stored = await database.StringGetAsync(cacheKey);
        using var state = JsonDocument.Parse(stored.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("messageName").GetString(), Is.EqualTo("ANSWER_TO_USER"));
            Assert.That(root.GetProperty("sessionId").GetString(), Is.EqualTo("sber-session"));
            Assert.That(root.GetProperty("messageId").GetInt64(), Is.EqualTo(42));
            Assert.That(root.GetProperty("uuid").GetProperty("userChannel").GetString(), Is.EqualTo("test-channel"));
            Assert.That(answer.GetProperty("device").GetProperty("platformType").GetString(), Is.EqualTo("test-platform"));
            Assert.That(answer.GetProperty("items")[0].GetProperty("bubble").GetProperty("text").GetString(),
                Does.StartWith("Чтобы сочинить историю"));
            Assert.That(answer.GetProperty("auto_listening").GetBoolean(), Is.True);
            Assert.That(answer.TryGetProperty("intent", out _), Is.False);
            Assert.That(state.RootElement.GetProperty("NextTextIndex").GetInt32(), Is.EqualTo(7));
            Assert.That(state.RootElement.GetProperty("RotationState").GetString(), Is.EqualTo("""{"Cycle":1,"Done":"AQ=="}"""));
            Assert.That(state.RootElement.GetProperty("ScopeKey").GetString(), Is.EqualTo("emulator"));
        });
    }

    private string GetWebhookPath(string platform)
    {
        var token = platform switch
        {
            "yandex" => _factory.Services.GetRequiredService<YandexConfiguration>().IncomingToken,
            "marusia" => _factory.Services.GetRequiredService<MarusiaConfiguration>().IncomingToken,
            "sber" => _factory.Services.GetRequiredService<SberConfiguration>().IncomingToken,
            _ => throw new ArgumentOutOfRangeException(nameof(platform))
        };
        return string.IsNullOrEmpty(token) ? $"/{platform}" : $"/{platform}/{Uri.EscapeDataString(token)}";
    }

    private async Task<JsonDocument> PostAsync(string path, object payload)
    {
        using var response = await _client.PostAsync(path, JsonContent.Create(payload));
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
        return JsonDocument.Parse(body);
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

    private static object CreatePayload(bool isNewSession, string command, string? sessionId = null, object? userState = null)
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
            state = new { session = new { }, user = userState ?? new { } },
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

    private sealed class StoryPayloadInterceptor : Interceptor
    {
        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            var response = await continuation(request, context);
            if (request is DetectIntentRequest { QueryInput.Text.Text: "5" } && response is DetectIntentResponse intent)
            {
                var payload = JsonParser.Default.Parse<Struct>("""
                    {"Yandex":{"buttons":[{"text":"Кнопка из payload","url":"https://example.test/story","isQuickReply":false}]}}
                    """);
                intent.QueryResult.FulfillmentMessages.Add(new Intent.Types.Message { Payload = payload });
            }
            return response;
        }
    }
}
