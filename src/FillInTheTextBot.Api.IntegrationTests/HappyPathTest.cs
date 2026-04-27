using System.Net;
using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Google.Cloud.Dialogflow.V2;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Api.IntegrationTests;

public class Tests
{
    private HttpClient? _client;
    private WebApplicationFactory<Program>? _factory;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        await EmulatorSetup();
        await RedisSetup();

        StartFitbWithWebApplicationFactory();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _client?.Dispose();
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }

        if (_redisContainer != null)
        {
            await _redisContainer.StopAsync();
            await _redisContainer.DisposeAsync();
        }

        if (_emulatorContainer != null)
        {
            await _emulatorContainer.StopAsync();
            await _emulatorContainer.DisposeAsync();
        }

        if (_emulatorImage != null)
        {
            await _emulatorImage.DeleteAsync().ConfigureAwait(false);
        }

        await SessionsClient.ShutdownDefaultChannelsAsync().ConfigureAwait(false);
        await ContextsClient.ShutdownDefaultChannelsAsync().ConfigureAwait(false);
    }

    private void StartFitbWithWebApplicationFactory()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("AppConfiguration:Dialogflow:0:EmulatorEndpoint", _emulatorEndpoint);
            builder.UseSetting("AppConfiguration:Redis:ConnectionString", _redisConnectionString);
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Debug);
            });
        });

        _client = _factory.CreateClient();
    }

    private IContainer? _emulatorContainer;
    private IFutureDockerImage? _emulatorImage;
    private const int EmulatorPort = 8080;
    private string? _emulatorEndpoint;

    private IContainer? _redisContainer;
    private const int RedisPort = 6379;
    private string? _redisConnectionString;

    private async Task RedisSetup()
    {
        _redisContainer = new ContainerBuilder("redis:7-alpine")
            .WithPortBinding(RedisPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
            .Build();

        await _redisContainer.StartAsync();

        var hostPort = _redisContainer.GetMappedPublicPort(RedisPort);
        _redisConnectionString = $"localhost:{hostPort}";
    }
    
    public async Task EmulatorSetup()
    {
        // Получаем путь к корню решения
        var solutionRoot = GetSolutionRoot();
        var dialogflowPath = Path.Combine(solutionRoot, "Dialogflow", "FillInTheTextBot-test-eu");
        var dockerfileDirectory = Path.Combine(solutionRoot, "src", "Dialogflow.Emulator");

        // Сначала собираем образ из Dockerfile
        // Добавляем уникальный идентификатор к имени образа для избежания конфликтов
        var imageTag = $"dialogflow-emulator-test:{Guid.NewGuid():N}";
        _emulatorImage = new ImageFromDockerfileBuilder()
            .WithDockerfile("Dockerfile")
            .WithDockerfileDirectory(dockerfileDirectory)
            .WithContextDirectory(solutionRoot)
            .WithName(imageTag)
            .WithCleanUp(true)
            .Build();

        await _emulatorImage.CreateAsync().ConfigureAwait(false);

        // Создаём контейнер с эмулятором
        _emulatorContainer = new ContainerBuilder(_emulatorImage)
            .WithPortBinding(EmulatorPort, true)
            .WithEnvironment("AGENT_PATH", "/app/agent")
            .WithEnvironment("Kestrel__Endpoints__Grpc__Url", "http://0.0.0.0:8080")
            .WithEnvironment("Kestrel__Endpoints__Grpc__Protocols", "Http2")
            .WithBindMount(dialogflowPath, "/app/agent")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Now listening on"))
            .Build();
        
        await _emulatorContainer.StartAsync();
        
        var hostPort = _emulatorContainer.GetMappedPublicPort(EmulatorPort);
        _emulatorEndpoint = $"localhost:{hostPort}";
    }

    private static string GetSolutionRoot()
    {
        var directory = TestContext.CurrentContext.TestDirectory;
        while (directory != null && !File.Exists(Path.Combine(directory, "FillInTheTextBot.slnx")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        if (directory == null)
        {
            throw new InvalidOperationException("Could not find solution root directory");
        }

        return directory;
    }
    
    private static object BuildYandexPayload(
        string sessionId,
        string skillId,
        string userId,
        string applicationId,
        bool isNewSession,
        string command,
        int messageId)
    {
        return new
        {
            meta = new
            {
                locale = "ru-RU",
                timezone = "UTC",
                client_id = $"client-{Guid.NewGuid():N}",
                interfaces = new
                {
                    screen = new { },
                    payments = new { },
                    account_linking = new { },
                    geolocation_sharing = new { }
                }
            },
            session = new
            {
                message_id = messageId,
                session_id = sessionId,
                skill_id = skillId,
                user = new { user_id = userId },
                application = new { application_id = applicationId },
                user_id = userId,
                @new = isNewSession
            },
            request = new
            {
                command,
                original_utterance = command,
                nlu = new
                {
                    tokens = Array.Empty<string>(),
                    entities = Array.Empty<object>(),
                    intents = new { }
                },
                markup = new { dangerous_context = false },
                type = "SimpleUtterance"
            },
            state = new
            {
                session = new { },
                user = new { },
                application = new { }
            },
            version = "1.0"
        };
    }

    private async Task<string> PostYandexAsync(object payload)
    {
        var response = await _client!.PostAsync("/yandex", JsonContent.Create(payload));
        var body = await response.Content.ReadAsStringAsync();
        TestContext.WriteLine($"Status: {response.StatusCode}");
        TestContext.WriteLine($"Body:   {body}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
        return body;
    }

    [Test]
    public async Task Happy_path_test()
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var skillId = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid().ToString("N");
        var applicationId = Guid.NewGuid().ToString("N");

        var welcomeBody = await PostYandexAsync(
            BuildYandexPayload(sessionId, skillId, userId, applicationId, isNewSession: true, command: string.Empty, messageId: 0));
        Assert.That(welcomeBody, Does.Contain("Добро пожаловать"),
            "Welcome event must trigger Default Welcome Intent");

        var startGameBody = await PostYandexAsync(
            BuildYandexPayload(sessionId, skillId, userId, applicationId, isNewSession: false, command: "да", messageId: 1));
        Assert.That(startGameBody, Does.Contain("время").Or.Contain("Класс").Or.Contain("Супер").Or.Contain("Отлично"),
            "After 'да' bot should proceed to start the game (EasyWelcome or Yes intent reply)");
    }
}