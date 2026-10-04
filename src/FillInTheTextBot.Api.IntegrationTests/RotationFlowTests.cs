using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FillInTheTextBot.Messengers.Yandex;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services.Rotation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FillInTheTextBot.Api.IntegrationTests;

/// <summary>
/// Ротация на боевом агенте: истории и правила берутся из файлов репозитория вместо GitHub.
/// </summary>
[TestFixture]
public class RotationFlowTests
{
    private const string AnotherStory = "давай другую историю";
    private const int RedisPort = 6379;

    private EmulatorFixture _emulator = null!;
    private IContainer _redis = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _yandexPath = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var dialogflowPath = Path.Combine(GetSolutionRoot(), "Dialogflow");
        var agentPath = Path.Combine(dialogflowPath, "FillInTheTextBot-eu");

        _emulator = new EmulatorFixture();
        await _emulator.StartAsync(agentPath);

        _redis = new ContainerBuilder("redis:alpine")
            .WithPortBinding(RedisPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
            .Build();

        await _redis.StartAsync();

        var source = new LocalRotationSource(Path.Combine(agentPath, "intents"), Path.Combine(dialogflowPath, "rotation.json"));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AppConfiguration:Dialogflow:0:ScopeId", "emulator");
            builder.UseSetting("AppConfiguration:Dialogflow:0:ProjectId", "fillinthetextbot-test");
            builder.UseSetting("AppConfiguration:Dialogflow:0:EmulatorEndpoint", _emulator.Endpoint);
            builder.UseSetting("AppConfiguration:Redis:ConnectionString", $"localhost:{_redis.GetMappedPublicPort(RedisPort)}");
            builder.UseSetting("AppConfiguration:Tracing:Port", "0");
            builder.UseSetting("AppConfiguration:Shutdown:DrainDelaySeconds", "0");
            builder.UseSetting("Logging:LogLevel:Default", "Warning");

            builder.ConfigureTestServices(services => services.AddSingleton<IRotationSource>(source));
        });

        _client = _factory.CreateClient();

        var incomingToken = _factory.Services.GetRequiredService<YandexConfiguration>().IncomingToken;

        _yandexPath = string.IsNullOrEmpty(incomingToken)
            ? "/yandex"
            : $"/yandex/{Uri.EscapeDataString(incomingToken)}";

        await WaitForRotationAsync(_factory.Services.GetRequiredService<RotationStore>());
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
    public async Task NewUser_CompletesStory_NextStoryOfFirstCycle()
    {
        var user = new YandexUser();

        await user.SayAsync(this, string.Empty);


        var first = await user.SayAsync(this, AnotherStory);
        var completed = await user.FillStoryAsync(this);
        var second = await user.SayAsync(this, AnotherStory);


        Assert.That(first.Current, Is.EqualTo(0), "Первый круг начинается с первой истории firstCycle");
        Assert.That(completed.IsDone(0), Is.True);
        Assert.That(completed.Current, Is.Null);
        Assert.That(second.Current, Is.EqualTo(37), "Следом — вторая история firstCycle");
    }

    [Test]
    public async Task UserAbandonsStory_ClosedAndNextIssued()
    {
        var user = new YandexUser();

        await user.SayAsync(this, string.Empty);
        await user.SayAsync(this, AnotherStory);


        var next = await user.SayAsync(this, AnotherStory);


        Assert.That(next.IsDone(0), Is.True, "Брошенная история закрыта на круг");
        Assert.That(next.Current, Is.EqualTo(37));
    }

    private async Task<JsonNode> PostYandexAsync(object payload)
    {
        var response = await _client.PostAsync(_yandexPath, JsonContent.Create(payload));

        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);

        return JsonNode.Parse(body)!;
    }

    private static async Task WaitForRotationAsync(RotationStore store)
    {
        for (var attempt = 0; attempt < 100 && store.Snapshot is null; attempt++)
        {
            await Task.Delay(100);
        }

        Assert.That(store.Snapshot, Is.Not.Null, store.LastCheck?.Error);
    }

    private static string GetSolutionRoot()
    {
        var directory = TestContext.CurrentContext.TestDirectory;

        while (directory is not null && !File.Exists(Path.Combine(directory, "src", "FillInTheTextBot.slnx")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        return directory ?? throw new InvalidOperationException("Не найден корень репозитория");
    }

    /// <summary>Пользователь Яндекса с сохраняемым между запросами user_state.</summary>
    private sealed class YandexUser
    {
        private readonly string _userId = Guid.NewGuid().ToString("N");
        private readonly string _sessionId = Guid.NewGuid().ToString("N");
        private readonly JsonObject _userState = new();

        private bool _isNewSession = true;

        public async Task<RotationState> SayAsync(RotationFlowTests tests, string command)
        {
            var payload = new
            {
                meta = new { locale = "ru-RU", timezone = "UTC", client_id = "integration-tests" },
                session = new
                {
                    message_id = 0,
                    session_id = _sessionId,
                    skill_id = "integration-tests",
                    user_id = _userId,
                    @new = _isNewSession
                },
                request = new
                {
                    command,
                    original_utterance = command,
                    type = "SimpleUtterance",
                    markup = new { dangerous_context = false },
                    nlu = new { tokens = Array.Empty<string>(), entities = Array.Empty<object>() }
                },
                state = new { session = new { }, user = _userState },
                version = "1.0"
            };

            _isNewSession = false;

            var body = await tests.PostYandexAsync(payload);

            foreach (var (key, value) in body["user_state_update"]!.AsObject())
            {
                _userState[key] = value?.DeepClone();
            }

            var rotationState = RotationState.Parse(_userState[Response.RotationStateStorageKey]?.GetValue<string>());

            return rotationState;
        }

        /// <summary>Отвечает на вопросы истории, пока она не будет дособрана.</summary>
        public async Task<RotationState> FillStoryAsync(RotationFlowTests tests)
        {
            for (var answer = 0; answer < 30; answer++)
            {
                var state = await SayAsync(tests, "кот");

                if (state.Current is null)
                {
                    return state;
                }
            }

            throw new InvalidOperationException("История не дособрана за 30 ответов");
        }
    }

    private sealed class LocalRotationSource(string intentsPath, string rulesPath) : IRotationSource
    {
        public Task<RotationSourceResult<string[]>> GetStoryFilesAsync(string etag, CancellationToken cancellationToken)
        {
            var files = Directory.GetFiles(intentsPath).Select(file => Path.GetFileName(file)).ToArray();

            return Task.FromResult(new RotationSourceResult<string[]>(true, files, null));
        }

        public Task<RotationSourceResult<string>> GetRulesAsync(string etag, CancellationToken cancellationToken)
        {
            var rules = File.ReadAllText(rulesPath);

            return Task.FromResult(new RotationSourceResult<string>(true, rules, null));
        }
    }
}
