using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services.Extensions;
using GranSteL.Helpers.Redis;

namespace FillInTheTextBot.Services
{
    public class ConversationService : IConversationService
    {
        const string NextTextIndexKey = "nextTextIndex";

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

            var response = new Response 
            { 
                Text = dialog?.Response, 
                Finished = dialog?.EndConversation ?? false,
                Buttons = dialog?.Buttons,
                ScopeKey = dialog?.ScopeKey

            };

            if (dialog?.ParametersIncomplete != true && string.Equals(dialog?.Action ?? string.Empty, "saveToRepeat", StringComparison.InvariantCultureIgnoreCase))
            {
                var parameters = new Dictionary<string, string>
                {
                    { "text", dialog?.Response }
                };

                _dialogflowService.SetContextAsync(request.SessionId, "savedText", 5, parameters).Forget();
            }

            var resetTextIndex = string.Empty;

            var isGotResetParameter = dialog?.Parameters.TryGetValue("resetTextIndex", out resetTextIndex) ?? false;

            if (isGotResetParameter && string.Equals(resetTextIndex, bool.TrueString, StringComparison.InvariantCultureIgnoreCase))
            {
                ResetLastTextIndexKey(request.UserHash);
            }

            if (string.Equals(dialog?.Action, "GetText"))
            {
                var textKey = dialog?.GetParameters("textKey").FirstOrDefault();

                response = await GetText(request, dialog?.Response, textKey);
            }

            var deleteAllContexts = string.Empty;

            var isGotDeleteParameter = dialog?.Parameters.TryGetValue("deleteAllContexts", out deleteAllContexts) ?? false;

            if (isGotDeleteParameter && string.Equals(deleteAllContexts, bool.TrueString, StringComparison.InvariantCultureIgnoreCase))
            {
                _dialogflowService.DeleteAllContextsAsync(request).Forget();
            }

            response.NextTextIndex = request.NextTextIndex;

            return response;
        }

        private async Task<Response> GetText(Request request, string startText, string textKey = null)
        {
            var response = new Response();

            if (string.IsNullOrEmpty(textKey))
            {
                if (!_cache.TryGet("Texts", out string[] texts))
                {
                    response.Text = "Что-то у меня не нашлось никаких текстов...";

                    return response;
                }

                var index = request.NextTextIndex++;

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
            response.Buttons = dialog?.Buttons;
            response.ScopeKey = dialog?.ScopeKey;

            return response;
        }

        /// <summary>
        /// На самом деле это индекс не последнего, а следующего текста
        /// </summary>
        /// <param name="userHash">Идентификатор пользователя</param>
        /// <returns>Ключ для получения индекса текста из кеша</returns>
        private string GetNextTextIndexCacheKey(string userHash)
        {
            return $"{NextTextIndexKey}:{userHash}";
        }

        private void ResetLastTextIndexKey(string userHash)
        {
            var lastTextIndexKey = GetNextTextIndexCacheKey(userHash);

            _cache.TryAddAsync(lastTextIndexKey, 0).Forget();
        }
    }
}
