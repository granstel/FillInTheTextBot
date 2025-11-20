using System.Net;
using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Api.IntegrationTests;

public class Tests
{
    private TestServer _server;
    private HttpClient _client;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        await EmulatorSetup();

        StartFitbWithWebApplicationFactory();
    }

    private void StartFitbWithTestServer()
    {
        Environment.SetEnvironmentVariable("AppConfiguration__Dialogflow__EmulatorEndpoint", _emulatorEndpoint);
        _server = new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder
                    .UseTestServer()
                    .ConfigureLogging(logging => logging.AddConsole())
                    .UseEnvironment("Development")
                    .UseStartup<Startup>();
            })
            .Build().GetTestServer();
        _client = _server.CreateClient();
    }

    private void StartFitbWithWebApplicationFactory()
    {
        // Environment.SetEnvironmentVariable("AppConfiguration__Dialogflow__EmulatorEndpoint", _emulatorEndpoint);

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("AppConfiguration:Dialogflow:0:EmulatorEndpoint", _emulatorEndpoint);
        });
        

        _client = factory.CreateClient();
    }

    private IContainer? _emulatorContainer;
    private IFutureDockerImage? _emulatorImage;
    private const int EmulatorPort = 8080;
    private string? _emulatorEndpoint;
    
    public async Task EmulatorSetup()
    {
        // Получаем путь к корню решения
        var solutionRoot = GetSolutionRoot();
        var dialogflowPath = Path.Combine(solutionRoot, "Dialogflow", "FillInTheTextBot-test-eu");
        var dockerfileDirectory = Path.Combine(solutionRoot, "src", "Dialogflow.Emulator");

        // Сначала собираем образ из Dockerfile
        // Добавляем уникальный идентификатор к имени образа для избежания конфликтов
        var imageTag = "dialogflow-emulator-test:latest";
        _emulatorImage = new ImageFromDockerfileBuilder()
            .WithDockerfile("Dockerfile")
            .WithDockerfileDirectory(dockerfileDirectory)
            .WithContextDirectory(solutionRoot)
            .WithName(imageTag)
            .Build();
        
         await _emulatorImage.CreateAsync().ConfigureAwait(false);

        // Создаём контейнер с эмулятором
        _emulatorContainer = new ContainerBuilder()
            .WithImage(_emulatorImage)
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
    
    [Test]
    public async Task Happy_path_test()
    {
        var rnd = new Random();

        var payload = new
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
                message_id = rnd.Next(0, 1000),
                session_id = Guid.NewGuid().ToString("N"),
                skill_id = Guid.NewGuid().ToString("N"),
                user = new { user_id = Guid.NewGuid().ToString("N") },
                application = new { application_id = Guid.NewGuid().ToString("N") },
                user_id = Guid.NewGuid().ToString("N"),
                @new = true
            },
            request = new
            {
                command = string.Empty,
                original_utterance = string.Empty,
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

        var jsonContent = JsonContent.Create(payload);
        var response = await _client.PostAsync("/yandex", jsonContent);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}