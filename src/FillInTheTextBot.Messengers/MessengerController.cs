using System;
using System.Linq;
using System.Threading.Tasks;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FillInTheTextBot.Messengers;

[Route("[controller]")]
[Produces("application/json")]
public abstract class MessengerController<TInput, TOutput>(
    ILogger log,
    IMessengerService<TInput, TOutput> messengerService,
    MessengerConfiguration configuration)
    : Controller
{
    private const string TokenParameter = "token";

    protected readonly ILogger Log = log;
    protected JsonSerializerSettings SerializerSettings;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var isValid = IsValidRequest(context);

        if (!isValid) context.Result = NotFound();
    }

    [HttpGet]
    public string GetInfo()
    {
        var url = GetWebHookUrl(Request);

        return $"{DateTime.Now:F} {url}";
    }

    [HttpPost("{token?}")]
    public virtual async Task<IActionResult> WebHook([FromBody] TInput input, string token)
    {
        if (!ModelState.IsValid)
        {
            var errors = GetErrors(ModelState);
            Log.LogError(errors);
        }

        var response = await messengerService.ProcessIncomingAsync(input);

        return Json(response, SerializerSettings);
    }

    [HttpPut("{token?}")]
    public virtual async Task<IActionResult> CreateWebHook(string token)
    {
        var url = GetWebHookUrl(Request);

        var result = await messengerService.SetWebhookAsync(url);

        return Json(result);
    }

    [HttpDelete("{token?}")]
    public virtual async Task<IActionResult> DeleteWebHook(string token)
    {
        var result = await messengerService.DeleteWebhookAsync();

        return Json(result);
    }

    protected virtual bool IsValidRequest(ActionExecutingContext context)
    {
        if (string.IsNullOrEmpty(configuration.IncomingToken)) return true;

        if (context.ActionArguments.TryGetValue(TokenParameter, out var value))
        {
            var token = value as string;

            return string.Equals(configuration.IncomingToken, token, StringComparison.InvariantCultureIgnoreCase);
        }

        return false;
    }

    private string GetWebHookUrl(HttpRequest request)
    {
        var pathBase = request.PathBase.Value;
        var pathSegment = request.Path.Value;

        var url = $"{request.Scheme}://{request.Host}{pathBase}{pathSegment}";

        return url;
    }

    private string GetErrors(ModelStateDictionary modelState)
    {
        return modelState?.Values
            .SelectMany(v => v.Errors?.Select(e => e.ErrorMessage))
            .Where(m => !string.IsNullOrEmpty(m))
            .JoinToString(Environment.NewLine);
    }
}