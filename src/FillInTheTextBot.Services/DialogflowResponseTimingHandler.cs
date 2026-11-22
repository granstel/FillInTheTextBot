using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FillInTheTextBot.Services;

public sealed class DialogflowResponseTimingHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        for (var activity = Activity.Current; activity is not null; activity = activity.Parent)
        {
            if (activity.Source.Name == Tracing.ActivitySourceName &&
                activity.OperationName == DialogflowService.DetectIntentActivityName)
            {
                if (activity.Recorded)
                {
                    return SendWithTimingAsync(activity, request, cancellationToken);
                }

                break;
            }
        }

        return base.SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendWithTimingAsync(Activity activity, HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.Content = new TimedContent(response.Content, activity);
        return response;
    }

    private sealed class TimedContent : HttpContent
    {
        private readonly HttpContent _content;
        private readonly Activity _activity;
        private readonly long _headers = Stopwatch.GetTimestamp();
        private readonly DateTimeOffset _headersAt = DateTimeOffset.UtcNow;
        private long _firstData;
        private long _lastData;
        private long _streamEnd;
        private int _published;

        public TimedContent(HttpContent content, Activity activity)
        {
            _content = content;
            _activity = activity;
            foreach (var header in content.Headers.NonValidated)
            {
                Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        protected override async Task<Stream> CreateContentReadStreamAsync() =>
            new TimedStream(await _content.ReadAsStreamAsync().ConfigureAwait(false), this);

        protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) =>
            new TimedStream(await _content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), this);

        protected override Stream CreateContentReadStream(CancellationToken cancellationToken) =>
            new TimedStream(_content.ReadAsStream(cancellationToken), this);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) =>
            _content.CopyToAsync(stream, context);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context,
            CancellationToken cancellationToken) => _content.CopyToAsync(stream, context, cancellationToken);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        private void Publish()
        {
            if (Interlocked.Exchange(ref _published, 1) != 0)
            {
                return;
            }

            var tags = new ActivityTagsCollection();
            AddTiming(tags, "first_data_ms", Volatile.Read(ref _firstData));
            AddTiming(tags, "last_data_ms", Volatile.Read(ref _lastData));
            AddTiming(tags, "stream_end_ms", Volatile.Read(ref _streamEnd));
            _activity.AddEvent(new ActivityEvent("dialogflow.response.read", _headersAt, tags));
        }

        private void AddTiming(ActivityTagsCollection tags, string name, long timestamp)
        {
            if (timestamp != 0)
            {
                tags.Add(name, Stopwatch.GetElapsedTime(_headers, timestamp).TotalMilliseconds);
            }
        }

        public int Observe(int count, int requested)
        {
            if (count > 0)
            {
                var timestamp = Stopwatch.GetTimestamp();
                if (_firstData == 0)
                {
                    Volatile.Write(ref _firstData, timestamp);
                }

                Volatile.Write(ref _lastData, timestamp);
            }
            else if (requested > 0)
            {
                Volatile.Write(ref _streamEnd, Stopwatch.GetTimestamp());
                Publish();
            }

            return count;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Publish();
                _content.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class TimedStream(Stream inner, TimedContent timing) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override int Read(byte[] buffer, int offset, int count) =>
            timing.Observe(inner.Read(buffer, offset, count), count);

        public override int Read(Span<byte> buffer) => timing.Observe(inner.Read(buffer), buffer.Length);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = inner.ReadAsync(buffer, cancellationToken);
            return read.IsCompletedSuccessfully
                ? ValueTask.FromResult(timing.Observe(read.Result, buffer.Length))
                : ObserveReadAsync(read, buffer.Length);
        }

        private async ValueTask<int> ObserveReadAsync(ValueTask<int> read, int requested) =>
            timing.Observe(await read.ConfigureAwait(false), requested);

        public override void Flush() => inner.Flush();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
