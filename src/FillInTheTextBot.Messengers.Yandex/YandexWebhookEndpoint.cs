using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Yandex.Dialogs.Models.Input;

namespace FillInTheTextBot.Messengers.Yandex;

public static class YandexWebhookEndpoint
{
    private const string WebhookRoute = "Yandex/{token?}";

    public static void MapYandexEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var json = endpoints.ServiceProvider.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;

        endpoints.MapGet("Yandex", IResult (HttpRequest request, YandexConfiguration configuration) =>
            string.IsNullOrEmpty(configuration.IncomingToken)
                ? Results.Json($"{DateTime.Now:F} {GetWebhookUrl(request)}", json)
                : Results.NotFound());

        endpoints.MapPost(WebhookRoute, async Task<IResult> (
            HttpRequest request, IYandexService service, YandexConfiguration configuration, ILogger<YandexService> log) =>
        {
            if (!HasValidToken(request, configuration))
            {
                return Results.NotFound();
            }

            var encoding = GetRequestEncoding(request.ContentType);
            if (encoding is null)
            {
                return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
            }

            InputModel input = null;
            try
            {
                input = await ReadInputAsync(request, encoding, json);
                if (input is null)
                {
                    log.LogError("A non-empty request body is required.");
                }
            }
            catch (Exception error) when (error is JsonException or FormatException or OverflowException)
            {
                log.LogError("{Error}", error.Message);
            }

            var response = await service.ProcessIncomingAsync(input);
            return Results.Json(response, json);
        });

        endpoints.MapPut(WebhookRoute, async Task<IResult> (
            HttpRequest request, IYandexService service, YandexConfiguration configuration) =>
            HasValidToken(request, configuration)
                ? Results.Json(await service.SetWebhookAsync(GetWebhookUrl(request)), json)
                : Results.NotFound());

        endpoints.MapDelete(WebhookRoute, async Task<IResult> (
            HttpRequest request, IYandexService service, YandexConfiguration configuration) =>
            HasValidToken(request, configuration)
                ? Results.Json(await service.DeleteWebhookAsync(), json)
                : Results.NotFound());
    }

    private static bool HasValidToken(HttpRequest request, YandexConfiguration configuration)
    {
        if (string.IsNullOrEmpty(configuration.IncomingToken))
        {
            return true;
        }

        var token = request.RouteValues["token"] as string ?? request.Query["token"].FirstOrDefault();
        return !string.IsNullOrWhiteSpace(token) &&
            string.Equals(configuration.IncomingToken, token, StringComparison.InvariantCultureIgnoreCase);
    }

    private static Encoding GetRequestEncoding(string value)
    {
        if (!MediaTypeHeaderValue.TryParse(value, out var contentType) ||
            !(contentType.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
              contentType.MediaType.Equals("text/json", StringComparison.OrdinalIgnoreCase) ||
              (contentType.Type.Equals("application", StringComparison.OrdinalIgnoreCase) &&
               contentType.Suffix.Equals("json", StringComparison.OrdinalIgnoreCase))))
        {
            return null;
        }

        var encoding = contentType.Charset.HasValue ? contentType.Encoding : Encoding.UTF8;
        return encoding?.CodePage == Encoding.UTF8.CodePage || encoding?.CodePage == Encoding.Unicode.CodePage
            ? encoding
            : null;
    }

    private static async ValueTask<InputModel> ReadInputAsync(
        HttpRequest request, Encoding encoding, JsonSerializerOptions json)
    {
        if (encoding.CodePage == Encoding.UTF8.CodePage)
        {
            return await JsonSerializer.DeserializeAsync<InputModel>(request.BodyReader, json, request.HttpContext.RequestAborted);
        }

        await using var utf8 = Encoding.CreateTranscodingStream(request.Body, encoding, Encoding.UTF8, leaveOpen: true);
        return await JsonSerializer.DeserializeAsync<InputModel>(utf8, json, request.HttpContext.RequestAborted);
    }

    private static string GetWebhookUrl(HttpRequest request)
    {
        return $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}";
    }
}
