using System.Threading.Tasks;
using FillInTheTextBot.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Api.Middleware;

public class MemoryMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MemoryMonitoringMiddleware> _logger;

    public MemoryMonitoringMiddleware(RequestDelegate next, ILogger<MemoryMonitoringMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.Request.Path.Value;
        
        MemoryDiagnostics.LogMemoryUsage($"Request start: {endpoint}");
        
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        finally
        {
            MemoryDiagnostics.LogMemoryUsage($"Request end: {endpoint}");
        }
    }
}