﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FillInTheTextBot.Api.Exceptions;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.Extensions;
using Microsoft.AspNetCore.Http;
using NLog;

namespace FillInTheTextBot.Api.Middleware
{
    public class HttpLogMiddleware
    {
        private const string QueryIdHeaderName = "X-Query-Id";
        private const string QueryIdLogProperty = "QueryId";

        private readonly RequestDelegate _next;
        private readonly HttpLogConfiguration _configuration;
        private readonly Logger _log = LogManager.GetCurrentClassLogger();

        public HttpLogMiddleware(RequestDelegate next, HttpLogConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
        }

        // ReSharper disable once UnusedMember.Global
        public async Task InvokeAsync(HttpContext context)
        {
            var queryId = Guid.NewGuid().ToString("N");

            _log.SetProperty(QueryIdLogProperty, queryId);

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

            var isExcludeEndpoint = _configuration.ExcludeEndpoints.Any(w =>
                context.Request.Path.Value.Contains(w, StringComparison.InvariantCultureIgnoreCase));

            if (isExcludeEndpoint)
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

        private async Task LogRequest(HttpRequest request)
        {
            var builder = new StringBuilder();

            _log.SetProperty("Type", "Request");

            var method = request.Method;
            var queryString = request.QueryString;


            builder.AppendLine($"{method} {request.Path}{queryString}");
            _log.SetProperty("Method", method);
            _log.SetProperty("QueryString", queryString.Value);

            var user = request.HttpContext?.User?.Identity?.Name;

            _log.SetProperty("User", user);
            builder.AppendLine($"User: {user}");

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

            _log.Info(message);

            ClearProperties(false);
        }

        private async Task LogResponse(HttpResponse response)
        {
            var builder = new StringBuilder();

            _log.SetProperty("Type", "Response");

            var statusCode = response.StatusCode;

            builder.AppendLine($"{statusCode}");
            _log.SetProperty("StatusCode", statusCode);

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

            _log.Info(message);

            ClearProperties(true);
        }

        private void AddHeaders(StringBuilder builder, IHeaderDictionary headers)
        {
            foreach (var header in headers)
            {
                builder.AppendLine($"{header.Key}: {header.Value.JoinToString(" ")}");
            }

            var dictionary = headers.ToDictionary(c => c.Key, c => c.Value.JoinToString(","));

            _log.SetProperty("Headers", dictionary);
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

                _log.SetProperty("Body", content);
            }
            catch (ExcludeBodyException)
            {
                throw;
            }
            catch (Exception e)
            {
                _log.Error(e, "Не удалось записать тело в лог");
            }
        }

        private void ClearProperties(bool clearRequestId)
        {
            _log.SetProperty("Type", null);
            _log.SetProperty("Headers", null);
            _log.SetProperty("Body", null);
            _log.SetProperty("Method", null);
            _log.SetProperty("QueryString", null);
            _log.SetProperty("StatusCode", null);
            _log.SetProperty("User", null);

            if (clearRequestId)
            {
                _log.SetProperty(QueryIdLogProperty, null);
            }
        }
    }
}
