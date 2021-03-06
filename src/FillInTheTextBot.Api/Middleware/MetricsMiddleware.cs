﻿using Microsoft.AspNetCore.Http;
using NLog;
using System;
using System.Threading.Tasks;
using OpenTracing;

namespace FillInTheTextBot.Api.Middleware
{
    public class MetricsMiddleware
    {
        private readonly Logger _log = LogManager.GetLogger(nameof(MetricsMiddleware));

        private readonly RequestDelegate _next;
        private readonly ITracer _tracer;

        public MetricsMiddleware(RequestDelegate next, ITracer tracer)
        {
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
                _log.Error(ex);

                throw;
            }
        }
    }
}
