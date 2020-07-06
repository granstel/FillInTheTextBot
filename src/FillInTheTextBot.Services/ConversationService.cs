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
            var dialog = await _dialogflowService.GetResponseAsync(request);

            var response = new Response { Text = dialog?.Response, Finished = dialog?.EndConversation ?? false };

            if (dialog.Parameters.TryGetValue("resetTextIndex", out var resetTextIndex) && string.Equals(resetTextIndex, bool.TrueString, StringComparison.InvariantCultureIgnoreCase))
            {
                var LastTextIndexKey = GetLastTextIndexKey(request.UserHash);

                _cache.TryAdd(LastTextIndexKey, 0);
            }

            if (string.Equals(dialog?.Action, "GetText"))
            {
                var textKey = dialog?.GetParameters("textKey").FirstOrDefault();

                response = await GetText(request, dialog.Response, textKey);
            }

            if (!dialog.ParametersIncomplete && string.Equals(dialog.Action, "DeleteAllContexts"))
            {
                await _dialogflowService.DeleteAllContexts(request);
            }

            return response;
        }

        private async Task<Response> GetText(Request request, string startText, string textKey = null)
        {
            var response = new Response();

            var LastTextIndexKey = GetLastTextIndexKey(request.UserHash);

            _cache.TryGet<int>(LastTextIndexKey, out var nextTextIndex);

            var index = nextTextIndex++;

            if (string.IsNullOrEmpty(textKey))
            {
                if (!_cache.TryGet<string[]>("Texts", out var texts))
                {
                    response.Text = "Что-то у меня не нашлось никаких текстов...";

                    return response;
                }

                if (index >= texts.Length)
                {
                    textKey = "texts-over";
                }
                else
                {
                    textKey = texts[index];
                }
            }

            var eventName = $"event:{textKey}";


            var dialog = await _dialogflowService.GetResponseAsync(eventName, request.SessionId, textKey);


            var textName = dialog?.GetParameters("text-name")?.FirstOrDefault();

            var text = $"{startText} {textName} {dialog?.Response}";

            response.Text = text;

            _cache.TryAdd(LastTextIndexKey, nextTextIndex);

            return response;
        }

        private string GetLastTextIndexKey(string userHash)
        {
            return $"LastTextIndex:{userHash}";
        }
    }
}
