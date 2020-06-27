using System;
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

            var response = new Response { Text = dialog.Response, Finished = dialog.EndConversation };

            if (string.Equals(dialog.Action, "GetText"))
            {
                response = await GetText(dialog.Response, request.SessionId);
            }

            return response;
        }

        private async Task<Response> GetText(string startText, string sessionId)
        {
            var response = new Response();

            if (!_cache.TryGet<string[]>("Texts", out var texts))
            {
                response.Text = "Что-то у меня не нашлось никаких текстов...";
            }

            var random = new Random();

            var index = random.Next(0, texts.Length-1);

            var eventName = $"event:{texts[index]}";


            var dialog = await _dialogflowService.GetResponseAsync(eventName, sessionId);


            var textName = dialog?.GetParameters("text-name")?.FirstOrDefault();

            var textWithName = string.Empty;

            if (!string.IsNullOrEmpty(textName))
            {
                textWithName = $"Текст называется \"{textName}\".";
            }

            var text = $"{startText} {textWithName} {dialog?.Response}";

            response.Text = text;

            return response;
        }
    }
}
