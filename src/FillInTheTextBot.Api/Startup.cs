using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using FillInTheTextBot.Api.DI;
using FillInTheTextBot.Api.Middleware;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FillInTheTextBot.Api;

public class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        _configuration = configuration;
        InternalLoggerFactory.Factory = loggerFactory;
    }

    // This method gets called by the runtime. Use this method to add services to the container.
    // ReSharper disable once UnusedMember.Global
    public void ConfigureServices(IServiceCollection services)
    {
        services
            .AddMvc()
            .AddNewtonsoftJson();

        // Configure OpenTelemetry
        var fullVersion = Assembly.GetExecutingAssembly().GetName().Version;
        var version = $"{fullVersion?.Major}.{fullVersion?.Minor}.{fullVersion?.Build}";

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService("FillInTheTextBot", serviceVersion: version);

        services.AddOpenTelemetry()
            .WithTracing(builder => builder
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(options =>
                {
                    var tracingConfig = _configuration.GetSection("Tracing").Get<TracingConfiguration>();
                    if (tracingConfig != null)
                    {
                        var host = string.IsNullOrEmpty(tracingConfig.Host) ? "localhost" : tracingConfig.Host;
                        var port = tracingConfig.Port > 0 ? tracingConfig.Port : 4317; // Стандартный порт OTLP
                        options.Endpoint = new Uri($"http://{host}:{port}");
                    }
                    else
                    {
                        // Значения по умолчанию, если конфигурация не найдена
                        options.Endpoint = new Uri("http://localhost:4317");
                    }
                }))
            .WithMetrics(builder => builder
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(MetricsCollector.MeterName)
                .AddPrometheusExporter());
        services.AddHttpLogging(o => { o.LoggingFields = HttpLoggingFields.All; });

        services.AddAppConfiguration(_configuration);
        services.AddInternalServices();
        services.AddExternalServices();
    }


    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    // ReSharper disable once UnusedMember.Global
    public void Configure(IApplicationBuilder app, AppConfiguration configuration)
    {
        // Регистрируем ActivityListener для нашего ActivitySource
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "FillInTheTextBot",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        app.UseMiddleware<ExceptionsMiddleware>();

        app.UseRouting();
        
        if (configuration.HttpLog.Enabled)
            app.UseWhen(context => configuration.HttpLog.IncludeEndpoints.Any(w =>
                    context.Request.Path.Value.Contains(w, StringComparison.InvariantCultureIgnoreCase)),
                a => { a.UseHttpLogging(); });

        app.UseEndpoints(e =>
        {
            e.MapControllers();
            e.MapPrometheusScrapingEndpoint();
        });
    }
}