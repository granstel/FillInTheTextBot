using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yandex.Dialogs.Models.Input;

namespace FillInTheTextBot.Messengers.Yandex;

public static class YandexWebhookEndpoint
{
    public static void MapYandexWebhook(this IEndpointRouteBuilder endpoints)
    {
        var services = endpoints.ServiceProvider;
        var formatter = services.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters
            .OfType<SystemTextJsonInputFormatter>().Single();
        var metadata = services.GetRequiredService<IModelMetadataProvider>().GetMetadataForType(typeof(InputModel));
        var readerFactory = services.GetRequiredService<IHttpRequestStreamReaderFactory>().CreateReader;

        endpoints.MapPost("Yandex/{token?}", async Task<IResult> (
            HttpRequest request, IYandexService service, YandexConfiguration configuration, ILogger<YandexController> log) =>
        {
            if (!string.IsNullOrEmpty(configuration.IncomingToken))
            {
                var token = request.RouteValues["token"] as string ?? request.Query["token"].FirstOrDefault();
                if (string.IsNullOrWhiteSpace(token) ||
                    !string.Equals(configuration.IncomingToken, token, StringComparison.InvariantCultureIgnoreCase))
                {
                    return Results.NotFound();
                }
            }

            // Keep the JSON formatter for the comparison with the MVC action pipeline.
            var modelState = new ModelStateDictionary();
            var context = new InputFormatterContext(request.HttpContext, string.Empty, modelState,
                metadata, readerFactory);
            if (!formatter.CanRead(context))
            {
                return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
            }

            var input = await formatter.ReadAsync(context);
            if (!input.IsModelSet)
            {
                var errors = modelState.Values.SelectMany(entry => entry.Errors).ToArray();
                if (errors.Any(error => error.Exception is UnsupportedContentTypeException))
                {
                    return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
                }

                var message = errors.Length == 0
                    ? metadata.ModelBindingMessageProvider.MissingRequestBodyRequiredValueAccessor()
                    : string.Join(Environment.NewLine, errors.Select(error => error.ErrorMessage));
                log.LogError("{Errors}", message);
            }

            var response = await service.ProcessIncomingAsync((InputModel)input.Model);
            return Results.Json(response, formatter.SerializerOptions);
        });
    }
}
