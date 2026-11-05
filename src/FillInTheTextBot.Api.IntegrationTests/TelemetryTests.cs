using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using FillInTheTextBot.Messengers.Yandex;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Trace;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Input;

namespace FillInTheTextBot.Api.IntegrationTests;

[TestFixture]
[NonParallelizable]
public class TelemetryTests
{
    private static readonly string[] NetworkSources =
    [
        "Experimental.System.Net.Http.Connections",
        "Experimental.System.Net.NameResolution",
        "Experimental.System.Net.Sockets",
        "Experimental.System.Net.Security"
    ];

    [Test]
    public async Task Webhook_ExportsSafeRouteWithoutSecretOrClientMetadata()
    {
        var exporter = new CaptureExporter();
        await using var factory = CreateFactory(enabled: true, exporter: exporter, enrich: true);
        using var client = factory.CreateClient();
        factory.Services.GetRequiredService<YandexConfiguration>().IncomingToken = "synthetic-expected-token";

        using var request = new HttpRequestMessage(HttpMethod.Post,
            "/Yandex/synthetic-webhook-secret?token=synthetic-query-secret")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.UserAgent.ParseAdd("synthetic-agent/1.0");
        using var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        // TestServer может вернуть ответ до завершения Activity запроса.
        var span = await exporter.ServerSpan.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Multiple(() =>
        {
            Assert.That(span.Tags["http.route"], Is.EqualTo("Yandex/{token?}"));
            Assert.That(span.Tags["http.request.method"], Is.EqualTo("POST"));
            Assert.That(span.Tags["http.response.status_code"], Is.EqualTo(404));
            Assert.That(span.Duration, Is.GreaterThan(TimeSpan.Zero));
            Assert.That(span.DisplayName, Is.EqualTo("POST Yandex/{token?}"));
            Assert.That(span.Tags.Keys, Does.Not.Contain("url.path"));
            Assert.That(span.Tags.Keys, Does.Not.Contain("url.query"));
            Assert.That(span.Tags.Keys, Does.Not.Contain("url.full"));
            Assert.That(span.Tags.Keys, Does.Not.Contain("http.url"));
            Assert.That(span.Tags.Keys, Does.Not.Contain("http.target"));
            Assert.That(span.Tags.Keys, Does.Not.Contain("client.address"));
            Assert.That(span.Tags.Keys, Does.Not.Contain("http.client_ip"));
            Assert.That(span.Tags.Keys, Does.Not.Contain("http.user_agent"));
            Assert.That(span.Tags.Keys, Does.Not.Contain("user_agent.original"));
            Assert.That(span.Tags.Keys, Does.Not.Contain("http.request.header.authorization"));
            Assert.That(string.Join(" ", span.Tags.Values) + span.DisplayName + span.StatusDescription,
                Does.Not.Contain("synthetic-"));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task HealthAndMetrics_AreNotTraced_ButKeepRequestMetrics(bool enabled)
    {
        var exporter = new CaptureExporter();
        await using var factory = CreateFactory(enabled, exporter: exporter);
        using var client = factory.CreateClient();

        using var health = await client.GetAsync("/health");
        var metrics = await client.GetStringAsync("/metrics");

        if (enabled)
        {
            // Проверяем сам фильтр, чтобы отсутствие spans не зависело от момента
            // завершения запроса в TestServer.
            var filter = factory.Services.GetRequiredService<IOptions<AspNetCoreTraceInstrumentationOptions>>().Value.Filter
                         ?? throw new AssertionException("Фильтр tracing должен быть настроен");
            var context = new DefaultHttpContext();
            foreach (var path in new[] { "/health", "/metrics", "/HEALTH", "/METRICS" })
            {
                context.Request.Path = path;
                Assert.That(filter(context), Is.False, path);
            }

            context.Request.Path = "/Yandex/synthetic-webhook-secret";
            Assert.That(filter(context), Is.True);
        }

        Assert.Multiple(() =>
        {
            Assert.That(health.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(exporter.Spans.Where(s => s.Kind == ActivityKind.Server), Is.Empty);
            Assert.That(metrics, Does.Contain("http_server_request_duration_seconds_count"));
            Assert.That(metrics, Does.Contain("http_route=\"/health\""));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task NetworkSources_RequireBothFlags(bool enabled, bool networkEnabled)
    {
        var exporter = new CaptureExporter();
        await using var factory = CreateFactory(enabled, networkEnabled, exporter);
        using var client = factory.CreateClient();

        foreach (var name in NetworkSources)
        {
            using var source = new ActivitySource(name);
            using var activity = source.StartActivity("synthetic-network-operation");
            Assert.That(activity is not null, Is.EqualTo(enabled && networkEnabled), name);
        }

        Assert.That(exporter.Spans.Count(s => NetworkSources.Contains(s.Source)),
            Is.EqualTo(enabled && networkEnabled ? NetworkSources.Length : 0));
    }

    [Test]
    public async Task Disabled_DoesNotCreateTracingProviderOrApplicationListener()
    {
        await using var factory = CreateFactory(enabled: false, networkEnabled: true);
        using var client = factory.CreateClient();
        using var source = new ActivitySource(Tracing.ActivitySourceName);

        Assert.Multiple(() =>
        {
            Assert.That(factory.Services.GetService<TracerProvider>(), Is.Null);
            Assert.That(source.HasListeners(), Is.False);
            Assert.That(new TracingConfiguration().NetworkEnabled, Is.False);
        });
    }

    [Test]
    public async Task OtlpExporter_UsesBoundedBackgroundBatchAndShortTimeout()
    {
        await using var factory = CreateFactory(enabled: true);
        using var client = factory.CreateClient();
        var options = factory.Services.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>().Get("tracing");

        Assert.Multiple(() =>
        {
            Assert.That(options.Protocol, Is.EqualTo(OtlpExportProtocol.Grpc));
            Assert.That(options.TimeoutMilliseconds, Is.EqualTo(3000));
            Assert.That(options.ExportProcessorType, Is.EqualTo(ExportProcessorType.Batch));
            Assert.That(options.BatchExportProcessorOptions.MaxQueueSize, Is.EqualTo(1024));
            Assert.That(options.BatchExportProcessorOptions.MaxExportBatchSize, Is.EqualTo(256));
            Assert.That(options.BatchExportProcessorOptions.ScheduledDelayMilliseconds, Is.EqualTo(1000));
            Assert.That(options.BatchExportProcessorOptions.ExporterTimeoutMilliseconds, Is.EqualTo(3000));
        });
    }

    private static WebApplicationFactory<Program> CreateFactory(bool enabled, bool networkEnabled = false,
        CaptureExporter? exporter = null, bool enrich = false)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AppConfiguration:Redis:ConnectionString", "localhost:6379");
            builder.UseSetting("AppConfiguration:Tracing:Enabled", enabled.ToString());
            builder.UseSetting("AppConfiguration:Tracing:NetworkEnabled", networkEnabled.ToString());
            builder.UseSetting("AppConfiguration:Tracing:Host", enabled ? "127.0.0.1" : "");
            builder.UseSetting("AppConfiguration:Tracing:Port", "1");
            builder.UseSetting("AppConfiguration:Shutdown:DrainDelaySeconds", "0");
            builder.UseSetting("AppConfiguration:Rotation:Enabled", "false");
            builder.UseSetting("AppConfiguration:HttpLog:Enabled", "false");
            builder.ConfigureServices(services =>
            {
                if (enabled && exporter is not null)
                {
                    // Снимок делается на границе экспорта, после production-процессора.
                    services.AddOpenTelemetry().WithTracing(tracing =>
                        tracing.AddProcessor(new SimpleActivityExportProcessor(exporter)));
                }

                if (enrich)
                {
                    services.AddSingleton<IYandexService, UnusedYandexService>();
                    services.Configure<AspNetCoreTraceInstrumentationOptions>(options =>
                        options.EnrichWithHttpResponse = (activity, _) =>
                        {
                            activity.SetTag("url.full", "https://synthetic-host/Yandex/synthetic-webhook-secret");
                            activity.SetTag("http.url", "https://synthetic-host/Yandex/synthetic-webhook-secret");
                            activity.SetTag("http.target", "/Yandex/synthetic-webhook-secret");
                            activity.SetTag("http.client_ip", "192.0.2.1");
                            activity.SetTag("client.address", "192.0.2.1");
                            activity.SetTag("http.user_agent", "synthetic-agent");
                            activity.SetTag("user_agent.original", "synthetic-agent");
                            activity.SetTag("http.request.header.authorization", "synthetic-authorization");
                            activity.SetStatus(ActivityStatusCode.Error, "synthetic-webhook-secret");
                        });
                }
            });
        });
    }

    private sealed class UnusedYandexService : IYandexService
    {
        public Task<OutputModel> ProcessIncomingAsync(InputModel input) => throw new AssertionException("Запрос должен быть отклонён до вызова сервиса");
        public Task<bool> SetWebhookAsync(string url) => throw new AssertionException("Неожиданный вызов сервиса");
        public Task<bool> DeleteWebhookAsync() => throw new AssertionException("Неожиданный вызов сервиса");
    }

    private sealed record ExportedSpan(string Source, ActivityKind Kind, string DisplayName,
        TimeSpan Duration, string? StatusDescription, Dictionary<string, object?> Tags);

    private sealed class CaptureExporter : BaseExporter<Activity>
    {
        public ConcurrentQueue<ExportedSpan> Spans { get; } = new();
        public TaskCompletionSource<ExportedSpan> ServerSpan { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
            {
                var span = new ExportedSpan(activity.Source.Name, activity.Kind, activity.DisplayName,
                    activity.Duration, activity.StatusDescription, activity.TagObjects.ToDictionary(t => t.Key, t => t.Value));
                Spans.Enqueue(span);
                if (activity.Kind == ActivityKind.Server)
                {
                    ServerSpan.TrySetResult(span);
                }
            }

            return ExportResult.Success;
        }
    }
}
