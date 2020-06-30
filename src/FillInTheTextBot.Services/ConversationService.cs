﻿using System;
using System.Linq;
using System.Threading.Tasks;
using FillInTheTextBot.Models.Internal;
using GranSteL.Helpers.Redis;

namespace FillInTheTextBot.Services
{
    public class ConversationService : IConversationService
    {
        private readonly IDialogflowService _dialogflowService;
        private readonly IRedisCacheService _cache;

        public ConversationService(IDialogflowService dialogflowService, IRedisCacheService cache)
        {
            _dialogflowService = dialogflowService;
            _cache = cache;
        }

        public async Task<Response> GetResponseAsync(Request request)
        {
            //TODO: processing commands, invoking external services, and other cool asynchronous staff to generate response
            var dialog = await _dialogflowService.GetResponseAsync(request);

            var response = new Response { Text = dialog?.Response, Finished = dialog?.EndConversation ?? false };

            if (string.Equals(dialog?.Action, "GetText"))
            {
                var textKey = dialog?.GetParameters("textKey").FirstOrDefault();

                response = await GetText(dialog.Response, request.SessionId, textKey);
            }

            if (!dialog.ParametersIncomplete && string.Equals(dialog.Action, "DeleteAllContexts"))
            {
                await _dialogflowService.DeleteAllContexts(request);
            }

            return response;
        }

        private async Task<Response> GetText(string startText, string sessionId, string textKey = null)
        {
            var response = new Response();

            if (string.IsNullOrEmpty(textKey))
            {
                if (!_cache.TryGet<string[]>("Texts", out var texts))
                {
                    response.Text = "Что-то у меня не нашлось никаких текстов...";
                }

                var random = new Random();

                var index = random.Next(0, texts.Length - 1);

                textKey = texts[index];
            }

            var eventName = $"event:{textKey}";


            var dialog = await _dialogflowService.GetResponseAsync(eventName, sessionId, textKey);


            var textName = dialog?.GetParameters("text-name")?.FirstOrDefault();

            var text = $"{startText} {textName} {dialog?.Response}";

            response.Text = text;

            return response;
        }
    }
}
