using System.Net;
using FillInTheTextBot.Api.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FillInTheTextBot.Api.IntegrationTests;

[TestFixture]
public class RotationEndpointTests
{
    private const int ServicePort = 8081;

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void InitTest()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AppConfiguration:ServicePort", ServicePort.ToString());
            builder.UseSetting("AppConfiguration:Redis:ConnectionString", "localhost:6379,abortConnect=false,connectTimeout=100");
            builder.UseSetting("AppConfiguration:Tracing:Port", "0");
            builder.UseSetting("AppConfiguration:Shutdown:DrainDelaySeconds", "0");
            builder.UseSetting("AppConfiguration:Rotation:Enabled", "false");
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
    public async Task Rotation_PublicPort_NotFound()
    {
        var response = await _client.GetAsync(RotationEndpoint.Path);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Rotation_ServicePort_Ok()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, RotationEndpoint.Path);
        request.Headers.Host = $"localhost:{ServicePort}";

        var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
    }
}
