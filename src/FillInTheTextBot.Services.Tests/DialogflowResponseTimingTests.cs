using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests;

public class DialogflowResponseTimingTests
{
    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task ResponseTiming_RecordsOnlyReachedStagesOnSampledSpans(bool recorded, bool readToEnd)
    {
        using var source = new ActivitySource(Tracing.ActivitySourceName);
        using var listener = new ActivityListener
        {
            ShouldListenTo = candidate => ReferenceEquals(candidate, source),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => recorded
                ? ActivitySamplingResult.AllDataAndRecorded
                : ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);
        using var activity = source.StartActivity("Dialogflow DetectIntent", ActivityKind.Internal,
            default(ActivityContext));

        using var content = new StreamContent(new MemoryStream(new byte[] { 42 }));
        content.Headers.ContentType = new("application/grpc");
        using var client = new HttpMessageInvoker(new DialogflowResponseTimingHandler(new ResponseHandler(content)));
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/synthetic");
        using var response = await client.SendAsync(request, CancellationToken.None);
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/grpc"));
        Assert.That(ReferenceEquals(response.Content, content), Is.EqualTo(!recorded));

        using var stream = await response.Content.ReadAsStreamAsync();
        Assert.That(await stream.ReadAsync(Memory<byte>.Empty), Is.Zero);
        var buffer = new byte[1];
        Assert.That(await stream.ReadAsync(buffer.AsMemory()), Is.EqualTo(1));
        Assert.That(buffer[0], Is.EqualTo(42));
        Assert.That(activity.Events, Is.Empty);

        if (readToEnd)
        {
            Assert.That(await stream.ReadAsync(buffer.AsMemory()), Is.Zero);
        }

        response.Dispose();
        if (!recorded)
        {
            Assert.That(activity.Events, Is.Empty);
            return;
        }

        var timing = activity.Events.Single();
        var tags = timing.Tags.ToDictionary(tag => tag.Key, tag => tag.Value);
        Assert.Multiple(() =>
        {
            Assert.That(timing.Name, Is.EqualTo("dialogflow.response.read"));
            Assert.That(tags.Keys, Is.EquivalentTo(readToEnd
                ? new[] { "first_data_ms", "last_data_ms", "stream_end_ms" }
                : new[] { "first_data_ms", "last_data_ms" }));
            Assert.That((double)tags["first_data_ms"], Is.GreaterThanOrEqualTo(0));
            Assert.That(tags["last_data_ms"], Is.EqualTo(tags["first_data_ms"]));
            if (readToEnd)
            {
                Assert.That((double)tags["stream_end_ms"], Is.GreaterThanOrEqualTo((double)tags["last_data_ms"]));
            }
        });
    }

    private sealed class ResponseHandler(HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
}
