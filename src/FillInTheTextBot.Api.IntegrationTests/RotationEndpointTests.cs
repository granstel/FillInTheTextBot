using System.Net;
using FillInTheTextBot.Api.Endpoints;
using Microsoft.AspNetCore.Http;
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
    public async Task Rotation_PublicPortWithServicePortInHost_NotFound()
    {
        var context = await SendAsync(localPort: 80, host: $"example.com:{ServicePort}");

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task Rotation_ServicePort_Ok()
    {
        var context = await SendAsync(localPort: ServicePort, host: "fitb");

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
    }

    private Task<HttpContext> SendAsync(int localPort, string host)
    {
        var context = _factory.Server.SendAsync(c =>
        {
            c.Request.Method = HttpMethods.Get;
            c.Request.Path = RotationEndpoint.Path;
            c.Request.Host = new HostString(host);
            c.Connection.LocalPort = localPort;
        });

        return context;
    }
}
