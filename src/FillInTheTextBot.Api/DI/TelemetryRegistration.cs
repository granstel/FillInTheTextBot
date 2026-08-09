using System;
using System.Reflection;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FillInTheTextBot.Api.DI
{
    internal static class TelemetryRegistration
    {
        private const int DefaultOtlpPort = 4317;

        internal static void AddTelemetry(this IServiceCollection services, TracingConfiguration tracing)
        {
            var assemblyName = Assembly.GetExecutingAssembly().GetName();
            var version = assemblyName.Version?.ToString(3);

            var otlpEndpoint = GetOtlpEndpoint(tracing);

            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(assemblyName.Name, serviceVersion: version))
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
        private static Uri GetOtlpEndpoint(TracingConfiguration tracing)
        {
            if (string.IsNullOrWhiteSpace(tracing?.Host))
            {
                return null;
            }

            var port = tracing.Port is > 0 ? tracing.Port.Value : DefaultOtlpPort;

            return new UriBuilder(Uri.UriSchemeHttp, tracing.Host, port).Uri;
        }
    }
}
