﻿using System;
using System.Linq;
using System.Threading.Tasks;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using NLog;

namespace FillInTheTextBot.Messengers
{
    [Route("[controller]")]
    public abstract class MessengerController<TInput, TOutput> : Controller
    {
        private readonly IMessengerService<TInput, TOutput> _messengerService;
        private readonly MessengerConfiguration _configuration;
        
        protected readonly Logger Log;
        protected JsonSerializerSettings SerializerSettings;

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
            var url = GetWebHookUrl(Request);

            return $"{DateTime.Now:F} {url}";
        }

        [HttpPost("{token?}")]
        public virtual async Task<IActionResult> WebHook([FromBody]TInput input, string token)
        {
            if (!ModelState.IsValid)
            {
                Log.Error(ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).JoinToString(Environment.NewLine));
            }

            var response = await _messengerService.ProcessIncomingAsync(input);

            return Json(response, SerializerSettings);
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
