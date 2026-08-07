using FillInTheTextBot.Api.Middleware;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;
using FillInTheTextBot.Api.DI;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FillInTheTextBot.Api
{
    public class Startup
    {
        private const int DefaultOtlpPort = 4317;

        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // ReSharper disable once UnusedMember.Global
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddMvc()
                .AddNewtonsoftJson();

            AddTelemetry(services);

            services.AddHttpLogging(o =>
            {
                o.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
            });

            services.AddAppConfiguration(_configuration);
            services.AddInternalServices();
            services.AddExternalServices();
        }

        private void AddTelemetry(IServiceCollection services)
        {
            var fullVersion = Assembly.GetExecutingAssembly().GetName().Version;
            var version = $"{fullVersion?.Major}.{fullVersion?.Minor}.{fullVersion?.Build}";

            var otlpEndpoint = GetOtlpEndpoint();

            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService("FillInTheTextBot", serviceVersion: version))
                .WithTracing(builder =>
                {
                    builder
                        // Без AddSource активности из Tracing создаются, но не экспортируются
                        .AddSource(Tracing.ActivitySourceName)
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation();

                    if (otlpEndpoint is not null)
                    {
                        builder.AddOtlpExporter(options => options.Endpoint = otlpEndpoint);
                    }
                })
                .WithMetrics(builder => builder
                    .AddMeter(MetricsCollector.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddPrometheusExporter());
        }

        /// <summary>
        /// Адрес OTLP-коллектора. Если хост не задан, экспорт трейсов не включается —
        /// иначе экспортёр будет циклически долбиться в несуществующий адрес.
        /// </summary>
        private Uri GetOtlpEndpoint()
        {
            // Значения читаются как строки, а не через Get<TracingConfiguration>: в шаблонном
            // appsettings.json Port пустой, и типизированная привязка на нём падает
            var tracing = _configuration.GetSection($"{nameof(AppConfiguration)}:{nameof(AppConfiguration.Tracing)}");

            var host = tracing[nameof(TracingConfiguration.Host)];

            if (string.IsNullOrWhiteSpace(host))
            {
                return null;
            }

            var port = int.TryParse(tracing[nameof(TracingConfiguration.Port)], out var configuredPort) && configuredPort > 0
                ? configuredPort
                : DefaultOtlpPort;

            var endpoint = new Uri($"http://{host}:{port}");

            return endpoint;
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        // ReSharper disable once UnusedMember.Global
        public void Configure(IApplicationBuilder app, AppConfiguration configuration)
        {
            app.UseMiddleware<ExceptionsMiddleware>();

            app.UseRouting();

            if (configuration.HttpLog.Enabled)
            {
                app.UseWhen(context => configuration.HttpLog.IncludeEndpoints.Any(w =>
                    context.Request.Path.Value.Contains(w, StringComparison.InvariantCultureIgnoreCase)), a =>
                    {
                        a.UseHttpLogging();
                    });
            }

            app.UseEndpoints(e =>
            {
                e.MapControllers();
                e.MapPrometheusScrapingEndpoint();
            });
        }
    }
}
