using System;
using System.Reflection;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Exporter;
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

            var telemetry = services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(assemblyName.Name, serviceVersion: version))
                .WithMetrics(builder => builder
                    .AddMeter(MetricsCollector.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter("System.Runtime")
                    .AddPrometheusExporter());

            if (tracing is { Enabled: true })
            {
                telemetry.WithTracing(builder =>
                {
                    builder
                        // Без AddSource активности из Tracing создаются, но не экспортируются
                        .AddSource(Tracing.ActivitySourceName)
                        .AddAspNetCoreInstrumentation(options => options.Filter = context =>
                            !context.Request.Path.StartsWithSegments(Startup.HealthPath,
                                StringComparison.OrdinalIgnoreCase)
                            && !context.Request.Path.StartsWithSegments("/metrics",
                                StringComparison.OrdinalIgnoreCase))
                        .AddHttpClientInstrumentation()
                        // OnEnd выполняется до постановки активности в очередь экспортёра.
                        .AddProcessor(new IncomingRequestTelemetryProcessor());

                    if (tracing.NetworkEnabled)
                    {
                        builder.AddSource(
                            "Experimental.System.Net.Http.Connections",
                            "Experimental.System.Net.NameResolution",
                            "Experimental.System.Net.Sockets",
                            "Experimental.System.Net.Security");
                    }

                    builder.AddOtlpExporter("tracing", options =>
                    {
                        options.Endpoint = otlpEndpoint;
                        options.Protocol = OtlpExportProtocol.Grpc;
                        options.TimeoutMilliseconds = 3000;
                        options.ExportProcessorType = ExportProcessorType.Batch;
                        options.BatchExportProcessorOptions = new BatchExportActivityProcessorOptions
                        {
                            MaxQueueSize = 1024,
                            MaxExportBatchSize = 256,
                            ScheduledDelayMilliseconds = 1000,
                            ExporterTimeoutMilliseconds = 3000
                        };
                    });
                });
            }
        }

        /// <summary>
        /// Адрес OTLP-коллектора. Трейсинг включается только явным флагом Enabled —
        /// иначе весь tracing pipeline не регистрируется; метрики работают независимо.
        /// </summary>
        private static Uri GetOtlpEndpoint(TracingConfiguration tracing)
        {
            if (tracing is not { Enabled: true })
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(tracing.Host))
            {
                throw new InvalidOperationException(
                    $"{nameof(TracingConfiguration)}.{nameof(TracingConfiguration.Host)} обязателен, " +
                    $"когда трейсинг включён ({nameof(TracingConfiguration.Enabled)} = true).");
            }

            var port = tracing.Port is > 0 ? tracing.Port.Value : DefaultOtlpPort;

            return new UriBuilder(Uri.UriSchemeHttp, tracing.Host, port).Uri;
        }
    }
}
