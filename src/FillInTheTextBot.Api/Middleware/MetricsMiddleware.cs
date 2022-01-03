using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using OpenTracing;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Api.Middleware
{
    public class MetricsMiddleware
    {
        private readonly ILogger<MetricsMiddleware> _log;

        private readonly RequestDelegate _next;
        private readonly ITracer _tracer;

        public MetricsMiddleware(ILogger<MetricsMiddleware> log, RequestDelegate next, ITracer tracer)
        {
            _log = log;
            _next = next;
            _tracer = tracer;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                using (var scope = _tracer.BuildSpan("waitingForValues").WithTag("test-tag","123").StartActive(finishSpanOnDispose: true))
                {
                    scope.Span.Log(DateTimeOffset.Now, "event");
                    await _next(context);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error");

                throw;
            }
        }
    }
}
