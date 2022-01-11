using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using FillInTheTextBot.Models;
using Google.Cloud.Dialogflow.V2;
using Google.Protobuf.WellKnownTypes;
using GranSteL.Helpers.Redis.Extensions;

namespace FillInTheTextBot.Services.Mapping
{
    public static class DialogflowMapping
    {
        public static Dialog ToDialog(this QueryResult s, Dialog d = null)
        {
            if (d == null)
            {
                d = new Dialog();
            }

            d.Parameters = GetParameters(s);
            d.Payload = ParsePayload(s);
            d.Response = s.FulfillmentText;
            d.Buttons = GetButtons(s);
            d.ParametersIncomplete = !s.AllRequiredParamsPresent;
            d.Action = s.Action;
            d.EndConversation = string.Equals(s?.Action, "endConversation");

            return d;
        }

        private static IDictionary<string, string> GetParameters(QueryResult queryResult)
        {
            var dictionary = new Dictionary<string, string>();

            var fields = queryResult?.Parameters.Fields;

            if (fields?.Any() != true)
            {
                return dictionary;
            }

            foreach (var field in fields)
            {
                if (field.Value.KindCase == Value.KindOneofCase.StringValue)
                {
                    dictionary.Add(field.Key, field.Value.StringValue);
                }
                else if (field.Value.KindCase == Value.KindOneofCase.StructValue)
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
            }

            return dictionary;
        }

        private static Button[] GetButtons(QueryResult s)
        {
            var quickReplies = s?.FulfillmentMessages
                            ?.Where(m => m.MessageCase == Intent.Types.Message.MessageOneofCase.QuickReplies)
                            .SelectMany(m => m.QuickReplies.QuickReplies_.Select(r => new Button
                            {
                                Text = r,
                                IsQuickReply = true
                            })).Where(r => r != null).ToList();

            var cards = s?.FulfillmentMessages
                            ?.Where(m => m.MessageCase == Intent.Types.Message.MessageOneofCase.Card)
                            .SelectMany(m => m.Card.Buttons.Select(b => new Button
                            {
                                Text = b.Text,
                                Url = b.Postback
                            })).Where(b => b != null).ToList();

            quickReplies.AddRange(cards);

            return quickReplies.ToArray();
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
