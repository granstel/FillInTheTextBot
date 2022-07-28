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

            TrySetSavedText(request.SessionId, dialog);

            var resetTextIndex = string.Empty;

            var isGotResetParameter = dialog?.Parameters.TryGetValue("resetTextIndex", out resetTextIndex) ?? false;

            if (isGotResetParameter && string.Equals(resetTextIndex, bool.TrueString, StringComparison.InvariantCultureIgnoreCase))
            {
                request.NextTextIndex = 0;
            }

            if (string.Equals(dialog?.Action, "GetText"))
            {
                var textKey = dialog.GetParameters("textKey").FirstOrDefault();

                response = await GetText(request, dialog.Response, textKey);
            }

            if (string.Equals(dialog?.Action, "CALL_RATING"))
            {
                response.Text = "CALL_RATING";
            }

            response.Emotions = GetEmotions(dialog);

            response.NextTextIndex = request.NextTextIndex;

            response.Text = GetResponseText(request.Appeal, response.Text);

            response.Buttons = AddButtonsFromPayload(response.Buttons, dialog?.Payload, request.Source);

            return response;
        }

        private async Task<Response> GetText(Request request, string startText, string textKey = null)
        {
            using (Tracing.Trace())
            {
                var response = new Response();

                if (string.IsNullOrEmpty(textKey))
                {
                    using (Tracing.Trace(operationName: "Get texts from cache"))
                    {
                        _cache.TryGet($"Texts-{request.Source}", out string[] texts);

                        if (texts?.Any() != true && !_cache.TryGet("Texts", out texts))
                        {
                            response.Text = "Что-то у меня не нашлось никаких текстов...";

                            return response;
                        }

                        textKey = GetTextKey(request, texts);
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
        }

        private string GetTextKey(Request request, string[] texts)
        {
            string textKey;

            var index = request.NextTextIndex++;

            if (index >= texts.Length)
            {
                textKey = "texts-over";
            }
            else
            {
                textKey = texts[index];
            }

            return textKey;
        }

        private IDictionary<string, string> GetEmotions(Dialog dialog)
        {
            using (Tracing.Trace())
            {
                var result = GetEmotionsFromDialog(dialog);

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
        }

        private Dictionary<string, string> GetEmotionsFromDialog(Dialog dialog)
        {
            using (Tracing.Trace())
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

                return result;
            }
        }

        private string GetResponseText(Appeal appeal, string responseText)
        {
            if (appeal != Appeal.Official)
            {
                return responseText;
            }

            using (Tracing.Trace())
            {
                _cache.TryGet($"AppealWords-{appeal}", out IDictionary<string, string> appealWords);

                if (appealWords?.Any() != true)
                {
                    return responseText;
                }

                using (Tracing.Trace(operationName: $"Replace words for appeal = '{appeal}'"))
                {
                    foreach (var appealWord in appealWords)
                    {
                        responseText = responseText.Replace(appealWord.Key, appealWord.Value);
                    }
                }
            }

            return responseText;
        }

        private ICollection<Button> AddButtonsFromPayload(ICollection<Button> responseButtons, Payload dialogPayload, Source requestSource)
        {
            using (Tracing.Trace())
            {
                var buttons = new List<Button>();

                if (responseButtons?.Any() == true)
                {
                    buttons.AddRange(responseButtons);
                }

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

        private void TrySetSavedText(string sessionId, Dialog dialog)
        {
            if (dialog?.ParametersIncomplete != true && string.Equals(dialog?.Action ?? string.Empty, "saveToRepeat", StringComparison.InvariantCultureIgnoreCase))
            {
                var parameters = new Dictionary<string, string>
                {
                    { "text", dialog?.Response }
                };

                _dialogflowService.SetContextAsync(sessionId, "savedText", 5, parameters).Forget();
            }
        }
    }
}
