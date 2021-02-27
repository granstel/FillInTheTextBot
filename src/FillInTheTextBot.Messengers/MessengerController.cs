using System;
using System.Threading.Tasks;
using FillInTheTextBot.Messengers.Extensions;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NLog;

namespace FillInTheTextBot.Messengers
{
    [Route("[controller]")]
    public abstract class MessengerController<TInput, TOutput> : Controller
    {
        private readonly IMessengerService<TInput, TOutput> _messengerService;
        private readonly MessengerConfiguration _configuration;
        
        protected readonly Logger Log;

        private const string TokenParameter = "token";

        protected MessengerController(IMessengerService<TInput, TOutput> messengerService, MessengerConfiguration configuration)
        {
            _messengerService = messengerService;
            _configuration = configuration;

            Log = LogManager.GetLogger(GetType().Name);
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
            var url = this.GetWebHookUrl();

            return $"{DateTime.Now:F} {url}";
        }

        [HttpPost("{token?}")]
        public virtual async Task<IActionResult> WebHook([FromBody]TInput input, string token)
        {
            var response = await _messengerService.ProcessIncomingAsync(input);

            return Json(response);
        }

        [HttpPut("{token?}")]
        public virtual async Task<IActionResult> CreateWebHook(string token)
        {
            var url = this.GetWebHookUrl();

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

            return true;
        }
    }
}
