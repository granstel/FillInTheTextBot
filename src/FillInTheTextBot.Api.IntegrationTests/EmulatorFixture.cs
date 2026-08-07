using System.Net;
using Dialogflow.Emulator.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Api.IntegrationTests;

/// <summary>
/// Поднимает эмулятор Dialogflow в том же процессе, что и тесты. Так не нужен
/// ни образ, ни докер — эмулятор всё равно самодостаточен и читает выгрузку
/// агента с диска.
/// </summary>
public sealed class EmulatorFixture : IAsyncDisposable
{
    private WebApplication? _app;

    public string Endpoint { get; private set; } = string.Empty;

    public async Task StartAsync(string agentPath)
    {
        var builder = WebApplication.CreateBuilder();

        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddGrpc();
        builder.Services.AddSingleton<IAgentStorage, AgentStorage>();

        // Порт 0 — операционная система выдаёт свободный, чтобы параллельные
        // прогоны не дрались за один и тот же
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2));

        _app = builder.Build();

        var storage = _app.Services.GetRequiredService<IAgentStorage>();
        await storage.InitializeAsync(agentPath);

        _app.MapGrpcService<SessionsEmulatorService>();
        _app.MapGrpcService<ContextsEmulatorService>();

        await _app.StartAsync();

        Endpoint = GetEndpoint(_app);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is null)
        {
            return;
        }

        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private static string GetEndpoint(WebApplication app)
    {
        var addresses = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()?.Addresses;

        var address = addresses?.FirstOrDefault()
                      ?? throw new InvalidOperationException("Эмулятор не сообщил адрес, на котором слушает");

        var uri = new Uri(address);

        return $"{uri.Host}:{uri.Port}";
    }
}
