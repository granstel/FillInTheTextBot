using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services.Extensions;
using FillInTheTextBot.Services.Mapping;
using GranSteL.Helpers.Redis;

namespace FillInTheTextBot.Services
{
    public class ConversationService : IConversationService
    {
        private static readonly Random Random = new Random();

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
                request.NextTextIndex = 0;
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

            response.Emotions = GetEmotions(dialog);

            response.NextTextIndex = request.NextTextIndex;

            var words = new string[0];
            words = dialog?.Payload?.Words?.GetValueOrDefault(request.Appeal, words) ?? words;

            response.Text = string.Format(response.Text ?? string.Empty, words);

            response.Buttons = AddButtonsFromPayload(response.Buttons, dialog?.Payload, request.Source);

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

        private IDictionary<string, string> GetEmotions(Dialog dialog)
        {
            var result = new Dictionary<string, string>();

            foreach (var emotionKey in EmotionsKeysMap.SourceEmotionKeys.Values)
            {
                var emotion = dialog?.GetParameters(emotionKey).FirstOrDefault();

                if (!string.IsNullOrEmpty(emotion))
                {
                    result.Add(emotionKey, emotion);
                }
            }

            var textName = dialog?.GetParameters("text-name")?.FirstOrDefault();

            if (result.Any() || dialog?.ParametersIncomplete == true || string.IsNullOrEmpty(textName))
            {
                return result;
            }

            foreach (var source in EmotionsToStoryMap.SourceEmotions.Keys)
            {
                var emotions = EmotionsToStoryMap.SourceEmotions[source];

                var emotion = emotions.OrderBy(x => Random.Next()).FirstOrDefault();

                var emotionKey = EmotionsKeysMap.SourceEmotionKeys[source];
                
                result.Add(emotionKey, emotion);
            }

            return result;
        }

        private ICollection<Button> AddButtonsFromPayload(ICollection<Button> responseButtons, Payload dialogPayload, Source requestSource)
        {
            var buttons = new List<Button>();
            buttons.AddRange(responseButtons);

            if (dialogPayload?.Buttons?.Any() == true)
            {
                buttons.AddRange(dialogPayload.Buttons);
            }

            var buttonsForSource = new List<Button>();
            buttonsForSource = dialogPayload?.ButtonsForSource?.GetValueOrDefault(requestSource, buttonsForSource).ToList() ?? buttonsForSource;

            buttons.AddRange(buttonsForSource);

            buttons = buttons.Where(b => !string.IsNullOrEmpty(b.Text)).ToList();

            return buttons;
        }
    }
}
