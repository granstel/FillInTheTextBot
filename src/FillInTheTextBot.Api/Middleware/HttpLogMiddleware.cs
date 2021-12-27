using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FillInTheTextBot.Api.Exceptions;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Api.Middleware
{
    public class HttpLogMiddleware
    {
        private const string QueryIdHeaderName = "X-Query-Id";
        private const string QueryIdLogProperty = "QueryId";

        private readonly RequestDelegate _next;
        private readonly HttpLogConfiguration _configuration;
        private readonly ILogger<HttpLogMiddleware> _log;

        public HttpLogMiddleware(ILogger<HttpLogMiddleware> log, RequestDelegate next, HttpLogConfiguration configuration)
        {
            _log = log;
            _next = next;
            _configuration = configuration;
        }

        // ReSharper disable once UnusedMember.Global
        public async Task InvokeAsync(HttpContext context)
        {
            var queryId = Guid.NewGuid().ToString("N");
            using (Tracing.Trace(s => s.WithTag(QueryIdLogProperty, queryId), "Http log"))
            {
                if (_configuration.AddRequestIdHeader)
                {
                    if (!context.Request.Headers.ContainsKey(QueryIdHeaderName))
                    {
                        context.Request.Headers.Add(QueryIdHeaderName, queryId);
                    }

                    if (!context.Response.Headers.ContainsKey(QueryIdHeaderName))
                    {
                        context.Response.Headers.Add(QueryIdHeaderName, queryId);
                    }
                }

                var isIncludeEndpoint = _configuration.IncludeEndpoints.Any(w =>
                    context.Request.Path.Value.Contains(w, StringComparison.InvariantCultureIgnoreCase));

                if (!isIncludeEndpoint)
                {
                    await _next(context);

                    return;
                }

                await LogRequest(context.Request);

                var responseBody = context.Response.Body;

                await using var stream = new MemoryStream();
                context.Response.Body = stream;

                try
                {
                    await _next(context);
                }
                finally
                {
                    await LogResponse(context.Response);

                    await stream.CopyToAsync(responseBody);

                    context.Response.Body = responseBody;
                }
            }
        }

        private async Task LogRequest(HttpRequest request)
        {
            var builder = new StringBuilder();

            builder.AppendLine("Request");

            var method = request.Method;
            var queryString = request.QueryString;


            builder.AppendLine($"{method} {request.Path}{queryString.Value}");

            AddHeaders(builder, request.Headers);

            try
            {
                if (request.ContentLength > 0)
                {
                    request.EnableBuffering();

                    await AddBodyAsync(builder, request.Body);
                }
            }
            catch (ExcludeBodyException)
            {
                //Exclude request from log
                return;
            }

            var message = builder.ToString();

            _log.LogInformation(message);
        }

        private async Task LogResponse(HttpResponse response)
        {
            var builder = new StringBuilder();

            builder.AppendLine("Response");

            builder.AppendLine($"{response.StatusCode}");

            builder.AppendLine($"{response.ContentType}");

            AddHeaders(builder, response.Headers);

            try
            {
                if (response.Body.Length > 0)
                {
                    response.Body.Seek(0, SeekOrigin.Begin);

                    await AddBodyAsync(builder, response.Body);
                }
            }
            catch (ExcludeBodyException)
            {
                //Exclude body from log
                return;
            }

            var message = builder.ToString();

            _log.LogInformation(message);
        }

        private void AddHeaders(StringBuilder builder, IHeaderDictionary headers)
        {
            foreach (var header in headers)
            {
                builder.AppendLine($"{header.Key}: {header.Value.JoinToString(" ")}");
            }
        }

        private async Task AddBodyAsync(StringBuilder builder, Stream body)
        {
            try
            {
                await using var memoryStream = new MemoryStream();
                await body.CopyToAsync(memoryStream);

                body.Seek(0, SeekOrigin.Begin);
                memoryStream.Seek(0, SeekOrigin.Begin);

                using var reader = new StreamReader(memoryStream);
                var content = await reader.ReadToEndAsync();

                if (_configuration.ExcludeBodiesWithWords.Any(w => content.Contains(w, StringComparison.InvariantCultureIgnoreCase)))
                {
                    throw new ExcludeBodyException();
                }

                builder.AppendLine("Body: ");
                builder.AppendLine(content);
            }
            catch (ExcludeBodyException)
            {
                throw;
            }
            catch (Exception e)
            {
                _log.LogError(e, "Error while add request body to log");
            }
        }
    }
}
