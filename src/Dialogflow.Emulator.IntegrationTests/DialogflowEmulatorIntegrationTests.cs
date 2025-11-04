namespace Dialogflow.Emulator.IntegrationTests;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Google.Cloud.Dialogflow.V2;
using NUnit.Framework;

[TestFixture]
public class DialogflowEmulatorIntegrationTests
{
    private IContainer? _emulatorContainer;
    private IFutureDockerImage? _emulatorImage;
    private const int EmulatorPort = 8080;
    private string? _emulatorEndpoint;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
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

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_emulatorContainer != null)
        {
            await _emulatorContainer.StopAsync();
            await _emulatorContainer.DisposeAsync();
        }

        if (_emulatorImage != null)
        {
            await _emulatorImage.DeleteAsync().ConfigureAwait(false);
        }

        // Ensure all default gRPC channels created by SessionsClient are shut down to avoid locked testhost processes.
        await SessionsClient.ShutdownDefaultChannelsAsync().ConfigureAwait(false);
    }

    [Test]
    public async Task DetectIntent_WelcomeEvent_ReturnsWelcomeMessage()
    {
        // Arrange
        var client = new SessionsClientBuilder
        {
            Endpoint = _emulatorEndpoint,
            ChannelCredentials = Grpc.Core.ChannelCredentials.Insecure
        }.Build();

        var sessionId = Guid.NewGuid().ToString();
        var sessionName = new SessionName("test-project", sessionId);

        var request = new DetectIntentRequest
        {
            SessionAsSessionName = sessionName,
            QueryInput = new QueryInput
            {
                Event = new EventInput
                {
                    Name = "WELCOME",
                    LanguageCode = "ru"
                }
            }
        };

        // Act
        var response = await client.DetectIntentAsync(request);

        // Assert
        Assert.That(response, Is.Not.Null);
        Assert.That(response.QueryResult, Is.Not.Null);
        Assert.That(response.QueryResult.Intent.DisplayName, Is.EqualTo("Default Welcome Intent"));
        Assert.That(response.QueryResult.FulfillmentText, Does.Contain("Добро пожаловать"));
        Assert.That(response.QueryResult.LanguageCode, Is.EqualTo("ru"));
    }

    [Test]
    public async Task DetectIntent_TextQuery_ReturnsMatchedIntent()
    {
        // Arrange
        var client = new SessionsClientBuilder
        {
            Endpoint = _emulatorEndpoint,
            ChannelCredentials = Grpc.Core.ChannelCredentials.Insecure
        }.Build();

        var sessionId = Guid.NewGuid().ToString();
        var sessionName = new SessionName("test-project", sessionId);

        var request = new DetectIntentRequest
        {
            SessionAsSessionName = sessionName,
            QueryInput = new QueryInput
            {
                Text = new TextInput
                {
                    Text = "да",
                    LanguageCode = "ru"
                }
            }
        };

        // Act
        var response = await client.DetectIntentAsync(request);

        // Assert
        Assert.That(response, Is.Not.Null);
        Assert.That(response.QueryResult, Is.Not.Null);
        Assert.That(response.QueryResult.QueryText, Is.EqualTo("да"));
        Assert.That(response.QueryResult.FulfillmentText, Is.Not.Empty);
    }

    [Test]
    public async Task DetectIntent_UnknownText_ReturnsFallbackIntent()
    {
        // Arrange
        var client = new SessionsClientBuilder
        {
            Endpoint = _emulatorEndpoint,
            ChannelCredentials = Grpc.Core.ChannelCredentials.Insecure
        }.Build();

        var sessionId = Guid.NewGuid().ToString();
        var sessionName = new SessionName("test-project", sessionId);

        var request = new DetectIntentRequest
        {
            SessionAsSessionName = sessionName,
            QueryInput = new QueryInput
            {
                Text = new TextInput
                {
                    Text = "абракадабра xyz 123",
                    LanguageCode = "ru"
                }
            }
        };

        // Act
        var response = await client.DetectIntentAsync(request);

        // Assert
        Assert.That(response, Is.Not.Null);
        Assert.That(response.QueryResult, Is.Not.Null);
        Assert.That(response.QueryResult.Intent.DisplayName, Is.EqualTo("Default Fallback Intent"));
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
}
