using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Api.Middleware;

public class ExceptionsMiddleware(ILogger<ExceptionsMiddleware> log, RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error while process request");

            throw;
        }
    }
}