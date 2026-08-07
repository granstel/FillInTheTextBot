using System.Net;
using FillInTheTextBot.Api.Health;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FillInTheTextBot.Api.IntegrationTests;

/// <summary>
/// Проверка готовности — основа бесшовного обновления: балансировщик выводит
/// экземпляр из ротации до того, как тот перестанет слушать порт.
/// </summary>
[TestFixture]
public class HealthTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void InitTest()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AppConfiguration:Redis:ConnectionString", "localhost:6379");
            builder.UseSetting("AppConfiguration:Tracing:Port", "0");
            // Пауза вывода из ротации нужна в бою, в тестах она только замедляет остановку
            builder.UseSetting("AppConfiguration:Shutdown:DrainDelaySeconds", "0");
            builder.UseSetting("Logging:LogLevel:Default", "Warning");
        });

        _client = _factory.CreateClient();
    }

    [TearDown]
    public async Task CleanUp()
    {
        _client.Dispose();

        await _factory.DisposeAsync();
    }

    [Test]
    public async Task Health_Running_Healthy()
    {
        var response = await _client.GetAsync(Startup.HealthPath);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("Healthy"));
    }

    [Test]
    public async Task Health_ShuttingDown_Unhealthy()
    {
        // Именно так делает GracefulShutdownService при получении сигнала остановки
        _factory.Services.GetRequiredService<ReadinessState>().BeginShutdown();

        var response = await _client.GetAsync(Startup.HealthPath);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable),
            "Пока приложение ещё принимает запросы, проверка здоровья должна уже краснеть");
    }
}
