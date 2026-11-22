using System;
using System.Linq;
using FillInTheTextBot.Api.DI;
using FillInTheTextBot.Api.Health;
using FillInTheTextBot.Api.Logging;
using FillInTheTextBot.Api.Middleware;
using FillInTheTextBot.Messengers.Yandex;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace FillInTheTextBot.Api;

public class Program
{
    public static int Main(string[] args)
    {
        try
        {
            Run(args);
            return 0;
        }
        catch (Exception exception) when (exception is not HostAbortedException)
        {
            AppConsoleFormatter.Write(Console.Out, LogLevel.Critical, nameof(Program),
                "Application host failed", exception);
            return 1;
        }
    }

    private static void Run(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.FormatterName = AppConsoleFormatter.FormatterName;
            options.LogToStandardErrorThreshold = LogLevel.None;
        }).AddConsoleFormatter<AppConsoleFormatter, ConsoleFormatterOptions>();

        var services = builder.Services;
        var appConfiguration = builder.Configuration.GetSection(nameof(AppConfiguration)).Get<AppConfiguration>();

        services.AddYandex(builder.Configuration);
        services.AddTelemetry(appConfiguration.Tracing);
        services.AddHttpLogging(options =>
        {
            options.LoggingFields = HttpLoggingFields.RequestMethod | HttpLoggingFields.ResponseStatusCode |
                HttpLoggingFields.Duration;
            options.CombineLogs = true;
        });

        services.AddAppConfiguration(appConfiguration);
        services.AddSingleton<ReadinessState>();
        services.AddHostedService<GracefulShutdownService>();
        services.AddHealthChecks().AddCheck<ReadinessHealthCheck>("readiness");
        services.AddOptions<HostOptions>()
            .Configure<ShutdownConfiguration>((options, shutdown) =>
                options.ShutdownTimeout = TimeSpan.FromSeconds(shutdown.TimeoutSeconds));

        services.AddInternalServices();
        services.AddExternalServices();
        services.AddRotation(appConfiguration.Rotation);

        var app = builder.Build();
        InternalLoggerFactory.Factory = app.Services.GetRequiredService<ILoggerFactory>();

        app.UseMiddleware<ExceptionsMiddleware>();
        app.UseRouting();

        var configuration = app.Services.GetRequiredService<AppConfiguration>();
        if (configuration.HttpLog.Enabled)
        {
            app.UseWhen(context => configuration.HttpLog.IncludeEndpoints.Any(endpoint =>
                context.Request.Path.Value.Contains(endpoint, StringComparison.InvariantCultureIgnoreCase)),
                branch => branch.UseHttpLogging());
        }

        app.MapYandexEndpoints();
        app.MapPrometheusScrapingEndpoint();
        app.MapHealthChecks(ReadinessHealthCheck.Route);
        app.Run();
    }
}
