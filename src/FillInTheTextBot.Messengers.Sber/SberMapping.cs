using System;
using System.Collections.Generic;
using System.Linq;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Mapping;
using Microsoft.Extensions.Logging;
using Sber.SmartApp.Models;
using Sber.SmartApp.Models.Constants;
using InternalModels = FillInTheTextBot.Models;
using SberModels = Sber.SmartApp.Models;

namespace FillInTheTextBot.Messengers.Sber
{
    public static class SberMapping
    {
        private static readonly ILogger Log;

        static SberMapping()
        {
            Log = InternalLoggerFactory.CreateLogger(typeof(SberMapping).Name);
        }

        public static InternalModels.Request ToRequest(this Request s)
        {
            if (s == null) return null;

            var d = new InternalModels.Request();

            d.ChatHash = s?.Payload?.AppInfo?.ProjectId.ToString();
            d.UserHash = s?.Uuid?.Sub ?? s?.Uuid?.UserId;
            d.Text = GetText(s);
            d.NewSession = s?.Payload?.NewSession;
            d.HasScreen = s?.Payload?.Device?.Capabilities?.Screen?.Available ?? false;
            d.ClientId = s?.Payload?.Device?.Surface;
            d.Source = InternalModels.Source.Sber;
            d.Appeal = GetAppeal(s);

            return d;
        }

        private static InternalModels.Appeal GetAppeal(Request s)
        {
            var appeal = InternalModels.Appeal.NoOfficial;

            if (string.Equals(s.Payload?.Character?.Appeal, "official"))
            {
                appeal = InternalModels.Appeal.Official;
            }

            return appeal;
        }

        private static string GetText(Request s)
        {
            const string replacedObsceneWord = "кое-что";
            const string stars = "***";

            var asrNormalizedMessage = s?.Payload?.Message?.AsrNormalizedMessage;

            try
            {
                if (asrNormalizedMessage?.Contains(stars) == true)
                {
                    return asrNormalizedMessage.Replace(stars, replacedObsceneWord);
                }

                var obsceneIndex = s?.Payload?.Annotations?.CensorData?.Classes?.ToList().IndexOf("obscene") ?? 0;

                if (s?.Payload?.Annotations?.CensorData?.Probas[obsceneIndex] == 1.0)
                {
                    return replacedObsceneWord;
                }
            }
            catch (Exception e)
            {
                Log.LogError(e, "Error while request mapping");
            }

            return s?.Payload?.Message?.OriginalText;
        }

        public static Response ToRespopnse (this InternalModels.Response s)
        {
            if (s == null) return null;

            var d = new Response();

            d.Payload = GetPayload(s);

            return d;
        }

        private static ResponsePayload GetPayload(InternalModels.Response s)
        {
            if (s == null) return null;

            var d = new ResponsePayload();

            d.PronounceText = s.Text;
            d.PronounceTextType = PronounceTextTypeValues.Text;
            d.AutoListening = !s.Finished;
            d.Finished = s.Finished;
            d.Emotion = GetEmotion(s);
            d.Items = GetPayloadItems(s);
            d.Suggestions = GetSuggestions(s.Buttons.Where(b => b.IsQuickReply));

            return d;
        }

        private static Suggestion GetSuggestions(IEnumerable<InternalModels.Button> sourceButtons)
        {
            var buttons = new List<Button>();

            foreach (var sourceButton in sourceButtons)
            {
                var button = new Button
                {
                    Title = sourceButton.Text,
                    Action = GetAction(sourceButton)
                };

                buttons.Add(button);
            }

            var suggest = new Suggestion
            {
                Buttons = buttons.ToArray()
            };

            return suggest;
        }

        private static PayloadItem[] GetPayloadItems(InternalModels.Response source)
        {
            var result = new List<PayloadItem>();

            var itemWithBubble = new PayloadItem
            {
                Bubble = { Text = source.Text }
            };

            result.Add(itemWithBubble);

            var buttons = source.Buttons?.Where(b => !b.IsQuickReply).ToList();

            if (buttons?.Any() != true)
            {
                return result.ToArray();
            }

            var cardItems = buttons?.Select(b =>
            {
                var cardItem = new CardCell
                {
                    Type = CellTypeValues.GreetingGridItem,
                    TopText = new CardCellText
                    {
                        Type = CellTypeValues.TextCellView,
                        Text = " ",
                        Typeface = TypefaceValues.Caption,
                        TextColor = TextColorValues.Default
                    },
                    BottomText = new CardCellText
                    {
                        Type = CellTypeValues.LeftRightCellView,
                        Text = b.Text,
                        Typeface = TypefaceValues.Button1,
                        TextColor = TextColorValues.Default,
                        MaxLines = 2,
                        Margins = new Margins
                        {
                            Top = IndentValues.X0
                        }
                    },
                    Paddings = new Paddings
                    {
                        Top = IndentValues.X0,
                        Left = IndentValues.X6,
                        Right = IndentValues.X6,
                        Bottom = IndentValues.X16
                    }
                };

                var action = GetAction(b);

                cardItem.Actions = new[] { action };

                return cardItem;
            }).ToArray();

            var card = new Card
            {
                Type = CardTypeValues.GridCard,
                Items = cardItems,
                Columns = 2,
                ItemWidth = ItemWidthValues.Resizable
            };

            itemWithBubble.Card = card;

            return result.ToArray();
        }

        private static SberModels.Action GetAction(InternalModels.Button s)
        {
            if (s == null) return null;

            var d = new SberModels.Action();

            d.Text = s.Text;
            d.DeepLink = s.Url;
            d.Type = GetActionType(s);

            return d;
        }

        private static string GetActionType(InternalModels.Button s)
        {
            if (!string.IsNullOrEmpty(s.Url))
            {
                return ActionTypeValues.DeepLink;
            }

            return ActionTypeValues.Text;
        }

        private static Emotion GetEmotion(InternalModels.Response s)
        {
            if (s == null) return null;

            var d = new Emotion();

            if (!s.Emotions.TryGetValue(EmotionsKeysMap.SourceEmotionKeys[InternalModels.Source.Sber], out string emotionKey))
            {
                return null;
            }

            d.EmotionId = emotionKey;

            return d;
        }

        public static Response FillResponse(this Request s, Response d)
        {
            if (s == null) return null;
            if (d == null)
            {
                d = new Response();
            }

            d.MessageName = MessageNameValues.AnswerToUser;
            d.SessionId = s.SessionId;
            d.MessageId = s.MessageId;
            d.Uuid = s.Uuid;

            if (d.Payload == null)
            {
                d.Payload = new ResponsePayload();
            }

            d.Payload.Device = s.Payload.Device;

            return d;
        }
    }
}
