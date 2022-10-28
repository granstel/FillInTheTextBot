using System.Collections.Generic;
using System.Linq;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services.Extensions;
using Google.Cloud.Dialogflow.V2;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

namespace FillInTheTextBot.Services.Mapping
{
    public static class DialogflowMapping
    {
        public static Dialog ToDialog(this QueryResult source, Dialog destination = null)
        {
            destination ??= new Dialog();

            destination.Parameters = GetParameters(source);
            destination.Payload = ParsePayload(source);
            destination.Response = source?.FulfillmentText;
            destination.Buttons = GetButtons(source);
            destination.ParametersIncomplete = !(source?.AllRequiredParamsPresent ?? false);
            destination.Action = source?.Action;
            destination.EndConversation = string.Equals(destination.Action, "endConversation");
            destination.CancelsSlotFilling = source?.CancelsSlotFilling ?? false;

            return destination;
        }

        private static IDictionary<string, string> GetParameters(QueryResult source)
        {
            var dictionary = new Dictionary<string, string>();

            var fields = source?.Parameters?.Fields;

            if (fields == null)
            {
                return dictionary;
            }

            dictionary = GetFieldsValues(fields);

            return dictionary;
        }

        private static Dictionary<string,string> GetFieldsValues(MapField<string,Value> fields)
        {
            var dictionary = new Dictionary<string, string>();

            foreach (var field in fields)
            {
                if (field.Value.KindCase == Value.KindOneofCase.StringValue)
                {
                    dictionary.Add(field.Key, field.Value.StringValue);

                    continue;
                }

                dictionary = GetStructFieldValues(field);
            }

            return dictionary;
        }

        private static Dictionary<string,string> GetStructFieldValues(KeyValuePair<string,Value> field)
        {
            var dictionary = new Dictionary<string, string>();

            if (field.Value.KindCase == Value.KindOneofCase.StructValue)
            {
                var stringValues = new List<string>();

                foreach (var valueField in field.Value.StructValue.Fields)
                {
                    if (valueField.Value.KindCase == Value.KindOneofCase.StringValue)
                    {
                        stringValues.Add(valueField.Value.StringValue);
                    }
                }

                var stringValue = string.Join("/", stringValues);

                dictionary.Add(field.Key, stringValue);
            }

            return dictionary;
        }

        private static Button[] GetButtons(QueryResult source)
        {
            var quickReplies = GetQuickReplies(source);

            var cards = GetCards(source);

            var buttons = quickReplies.Concat(cards);

            return buttons.ToArray();
        }

        private static Payload ParsePayload(QueryResult source)
        {
            var payload = source?.FulfillmentMessages?
                .Where(m => m.MessageCase == Intent.Types.Message.MessageOneofCase.Payload)
                .Select(m => m.Payload.ToString().Deserialize<Payload>()).FirstOrDefault();

            return payload;
        }

        private static ICollection<Button> GetQuickReplies(QueryResult source)
        {
            var quickReplies = source?.FulfillmentMessages
                ?.Where(m => m.MessageCase == Intent.Types.Message.MessageOneofCase.QuickReplies)
                .SelectMany(m => m.QuickReplies.QuickReplies_.Select(r => new Button
                {
                    Text = r,
                    IsQuickReply = true
                })).Where(r => r != null).ToList() ?? new List<Button>();

            return quickReplies;
        }

        private static ICollection<Button> GetCards(QueryResult source)
        {
            var cards = source?.FulfillmentMessages
                .OrderBy(m => m.Platform)
                ?.Where(m => m.MessageCase == Intent.Types.Message.MessageOneofCase.Card)
                .SelectMany(m => m.Card.Buttons.Select(b => new Button
                {
                    Text = b.Text,
                    Url = b.Postback
                })).Where(b => b != null).ToList()  ?? new List<Button>();

            return cards;
        }
    }
}
