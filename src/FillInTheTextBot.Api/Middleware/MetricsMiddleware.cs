using Microsoft.AspNetCore.Http;
using NLog;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace FillInTheTextBot.Api.Middleware
{
    public class MetricsMiddleware
    {
        private readonly Logger _log = LogManager.GetLogger(nameof(MetricsMiddleware));

        private readonly RequestDelegate _next;
        private readonly Stopwatch _stopwatch;

        public MetricsMiddleware(RequestDelegate next)
        {
            _next = next;

            _stopwatch = new Stopwatch();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            _stopwatch.Restart();

            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _log.Error(ex);

                throw;
            }
            finally
            {
                _stopwatch.Stop();
            }
        }
    }
}
