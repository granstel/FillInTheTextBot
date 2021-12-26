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

namespace FillInTheTextBot.Messengers
{
    [Route("[controller]")]
    public abstract class MessengerController<TInput, TOutput> : Controller
    {
        private readonly IMessengerService<TInput, TOutput> _messengerService;
        private readonly MessengerConfiguration _configuration;
        
        protected readonly ILogger Log;
        protected JsonSerializerSettings SerializerSettings;

        private const string TokenParameter = "token";

        protected MessengerController(ILogger log, IMessengerService<TInput, TOutput> messengerService, MessengerConfiguration configuration)
        {
            Log = log;
            _messengerService = messengerService;
            _configuration = configuration;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var isValid = IsValidRequest(context);

            if (!isValid)
            {
                context.Result = NotFound();
            }
        }

        [HttpGet]
        public string GetInfo()
        {
            var url = GetWebHookUrl(Request);

            return $"{DateTime.Now:F} {url}";
        }

        [HttpPost("{token?}")]
        public virtual async Task<IActionResult> WebHook([FromBody]TInput input, string token)
        {
            if (!ModelState.IsValid)
            {
                var errors = GetErrors(ModelState);
                Log.LogError(errors);
            }

            var response = await _messengerService.ProcessIncomingAsync(input);

            return Json(response, SerializerSettings);
        }

        private string GetErrors(ModelStateDictionary modelState)
        {
            return ModelState?.Values
                .SelectMany(v => v.Errors?.Select(e => e.ErrorMessage))
                .Where(m => !string.IsNullOrEmpty(m))
                .JoinToString(Environment.NewLine);
        }

        [HttpPut("{token?}")]
        public virtual async Task<IActionResult> CreateWebHook(string token)
        {
            var url = GetWebHookUrl(Request);

            var result = await _messengerService.SetWebhookAsync(url);

            return Json(result);
        }

        [HttpDelete("{token?}")]
        public virtual async Task<IActionResult> DeleteWebHook(string token)
        {
            var result = await _messengerService.DeleteWebhookAsync();

            return Json(result);
        }

        protected virtual bool IsValidRequest(ActionExecutingContext context)
        {
            if (string.IsNullOrEmpty(_configuration.IncomingToken))
            {
                return true;
            }

            if (context.ActionArguments.TryGetValue(TokenParameter, out object value))
            {
                var token = value as string;

                return string.Equals(_configuration.IncomingToken, token, StringComparison.InvariantCultureIgnoreCase);
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
    }
}
