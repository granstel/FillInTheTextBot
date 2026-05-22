using System;
using System.Collections.Generic;
using System.Linq;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Mapping;
using Microsoft.Extensions.Logging;
using Sber.SmartApp.Models.Constants;
using InternalModels = FillInTheTextBot.Models;
using SberModels = Sber.SmartApp.Models;

namespace FillInTheTextBot.Messengers.Sber;

public static class SberMapping
{
    private static readonly ILogger Log;

    static SberMapping()
    {
        Log = InternalLoggerFactory.CreateLogger(typeof(SberMapping).Name);
    }

    public static InternalModels.Request ToRequest(this SberModels.Request source)
    {
        if (source == null) return null;

        var destination = new InternalModels.Request();

        destination.ChatHash = source.Payload?.AppInfo?.ProjectId.ToString();
        destination.UserHash = source.Uuid?.Sub ?? source.Uuid?.UserId;
        destination.Text = GetText(source);
        destination.NewSession = source.Payload?.NewSession;
        destination.HasScreen = source.Payload?.Device?.Capabilities?.Screen?.Available ?? false;
        destination.ClientId = source.Payload?.Device?.Surface;
        destination.Source = InternalModels.Source.Sber;
        destination.Appeal = GetAppeal(source);

        return destination;
    }

    private static InternalModels.Appeal GetAppeal(SberModels.Request source)
    {
        var appeal = InternalModels.Appeal.NoOfficial;

        if (string.Equals(source.Payload?.Character?.Appeal, "official")) appeal = InternalModels.Appeal.Official;

        return appeal;
    }

    private static string GetText(SberModels.Request source)
    {
        const string replacedObsceneWord = "кое-что";
        const string stars = "***";

        var asrNormalizedMessage = source.Payload?.Message?.AsrNormalizedMessage;

        try
        {
            if (string.Equals(source.MessageName, "RATING_RESULT")) return "event:rating_result";

            if (asrNormalizedMessage?.Contains(stars) == true)
                return asrNormalizedMessage.Replace(stars, replacedObsceneWord);

            var obsceneIndex = source.Payload?.Annotations?.CensorData?.Classes?.ToList().IndexOf("obscene") ?? 0;

            if (source.Payload?.Annotations?.CensorData?.Probas[obsceneIndex] == 1.0) return replacedObsceneWord;
        }
        catch (Exception e)
        {
            Log.LogError(e, "Error while request mapping");
        }

        return source.Payload?.Message?.OriginalText;
    }

    public static SberModels.Response ToResponse(this InternalModels.Response source)
    {
        if (source == null) return null;

        var destination = new SberModels.Response();

        destination.MessageName = MessageNameValues.AnswerToUser;

        if (string.Equals(source.Text, "CALL_RATING")) destination.MessageName = "CALL_RATING";

        destination.Payload = GetPayload(source);

        return destination;
    }

    private static SberModels.ResponsePayload GetPayload(InternalModels.Response source)
    {
        if (source == null) return null;

        var d = new SberModels.ResponsePayload();

        d.PronounceText = source.AlternativeText;
        d.PronounceTextType = PronounceTextTypeValues.Ssml;
        d.AutoListening = !source.Finished;
        d.Finished = source.Finished;
        d.Emotion = GetEmotion(source);
        d.Items = GetPayloadItems(source);
        d.Suggestions = GetSuggestions(source.Buttons.Where(b => b.IsQuickReply));

        return d;
    }

    private static SberModels.Suggestion GetSuggestions(IEnumerable<InternalModels.Button> sourceButtons)
    {
        var buttons = new List<SberModels.Button>();

        foreach (var sourceButton in sourceButtons)
        {
            var button = new SberModels.Button
            {
                Title = sourceButton.Text,
                Action = GetAction(sourceButton)
            };

            buttons.Add(button);
        }

        var suggest = new SberModels.Suggestion
        {
            Buttons = buttons.ToArray()
        };

        return suggest;
    }

    private static SberModels.PayloadItem[] GetPayloadItems(InternalModels.Response source)
    {
        var result = new List<SberModels.PayloadItem>();

        var itemWithBubble = new SberModels.PayloadItem
        {
            Bubble = { Text = source.Text }
        };

        result.Add(itemWithBubble);

        var buttons = source.Buttons?.Where(b => !b.IsQuickReply).ToList();

        if (buttons?.Any() != true) return result.ToArray();

        var cardItems = buttons?.Select(b =>
        {
            var cardItem = new SberModels.CardCell
            {
                Type = CellTypeValues.GreetingGridItem,
                TopText = new SberModels.CardCellText
                {
                    Type = CellTypeValues.TextCellView,
                    Text = " ",
                    Typeface = TypefaceValues.Caption,
                    TextColor = TextColorValues.Default
                },
                BottomText = new SberModels.CardCellText
                {
                    Type = CellTypeValues.LeftRightCellView,
                    Text = b.Text,
                    Typeface = TypefaceValues.Button1,
                    TextColor = TextColorValues.Default,
                    MaxLines = 2,
                    Margins = new SberModels.Margins
                    {
                        Top = IndentValues.X0
                    }
                },
                Paddings = new SberModels.Paddings
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

        var card = new SberModels.Card
        {
            Type = CardTypeValues.GridCard,
            Items = cardItems,
            Columns = 2,
            ItemWidth = ItemWidthValues.Resizable
        };

        itemWithBubble.Card = card;

        return result.ToArray();
    }

    private static SberModels.Action GetAction(InternalModels.Button source)
    {
        if (source == null) return null;

        var d = new SberModels.Action();

        d.Text = source.Text;
        d.DeepLink = source.Url;
        d.Type = GetActionType(source);

        return d;
    }

    private static string GetActionType(InternalModels.Button source)
    {
        if (!string.IsNullOrEmpty(source.Url)) return ActionTypeValues.DeepLink;

        return ActionTypeValues.Text;
    }

    private static SberModels.Emotion GetEmotion(InternalModels.Response source)
    {
        if (source == null) return null;

        var d = new SberModels.Emotion();

        if (!source.Emotions.TryGetValue(EmotionsKeysMap.SourceEmotionKeys[InternalModels.Source.Sber],
                out var emotionKey)) return null;

        d.EmotionId = emotionKey;

        return d;
    }

    public static SberModels.Response FillResponse(this SberModels.Request source, SberModels.Response destination)
    {
        if (source == null) return null;
        if (destination == null) destination = new SberModels.Response();

        destination.SessionId = source.SessionId;
        destination.MessageId = source.MessageId;
        destination.Uuid = source.Uuid;

        if (destination.Payload == null) destination.Payload = new SberModels.ResponsePayload();

        destination.Payload.Device = source.Payload.Device;

        return destination;
    }
}