using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using FillInTheTextBot.Models;
using Google.Cloud.Dialogflow.V2;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using GranSteL.Helpers.Redis.Extensions;

namespace FillInTheTextBot.Services.Mapping
{
    public static class DialogflowMapping
    {
        public static Dialog ToDialog(this QueryResult source, Dialog destination = null)
        {
            if (destination == null)
            {
                destination = new Dialog();
            }

            destination.Parameters = GetParameters(source);
            destination.Payload = ParsePayload(source);
            destination.Response = source.FulfillmentText;
            destination.Buttons = GetButtons(source);
            destination.ParametersIncomplete = !source.AllRequiredParamsPresent;
            destination.Action = source.Action;
            destination.EndConversation = string.Equals(source?.Action, "endConversation");

            return destination;
        }

        private static IDictionary<string, string> GetParameters(QueryResult queryResult)
        {
            var dictionary = new Dictionary<string, string>();

            var fields = queryResult?.Parameters.Fields;

            if (fields?.Any() != true)
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

        private static Button[] GetButtons(QueryResult s)
        {
            var buttons = new List<Button>();

            var quickReplies = s?.FulfillmentMessages
                            ?.Where(m => m.MessageCase == Intent.Types.Message.MessageOneofCase.QuickReplies)
                            .SelectMany(m => m.QuickReplies.QuickReplies_.Select(r => new Button
                            {
                                Text = r,
                                IsQuickReply = true
                            })).Where(r => r != null).ToList();

            if (quickReplies?.Any() == true)
            {
                buttons.AddRange(quickReplies);
            }

            var cards = s?.FulfillmentMessages
                            ?.Where(m => m.MessageCase == Intent.Types.Message.MessageOneofCase.Card)
                            .SelectMany(m => m.Card.Buttons.Select(b => new Button
                            {
                                Text = b.Text,
                                Url = b.Postback
                            })).Where(b => b != null).ToList();

            if (cards?.Any() == true)
            {
                buttons.AddRange(cards);
            }

            return buttons.ToArray();
        }

        private static Payload ParsePayload(QueryResult queryResult)
        {
            var payload = queryResult?.FulfillmentMessages?
                .Where(m => m.MessageCase == Intent.Types.Message.MessageOneofCase.Payload)
                .Select(m => m.Payload.ToString().Deserialize<Payload>()).FirstOrDefault();

            return payload;
        }
    }
}
