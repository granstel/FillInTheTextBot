using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Google.Cloud.Dialogflow.V2;
using Google.Protobuf.WellKnownTypes;
using FillInTheTextBot.Models;
using GranSteL.Helpers.Redis.Extensions;

namespace FillInTheTextBot.Services.Mapping
{
    public class DialogflowProfile : Profile
    {
        public DialogflowProfile()
        {
            CreateMap<QueryResult, Dialog>()
                .ForMember(d => d.Parameters, m => m.MapFrom(s => GetParameters(s)))
                .ForMember(d => d.Response, m => m.MapFrom(s => s.FulfillmentText))
                .ForMember(d => d.Payload, m => m.MapFrom(s => GetPayload(s)))
                .ForMember(d => d.ParametersIncomplete, m => m.MapFrom(s => !s.AllRequiredParamsPresent))
                .ForMember(d => d.Action, m => m.MapFrom(s => s.Action))
                .ForMember(d => d.EndConversation, m => m.Ignore())
                .AfterMap((s, d) =>
                {
                    d.EndConversation = s.DiagnosticInfo?.Fields?.Where(f => string.Equals(f.Key, "end_conversation"))
                                            .Select(f => f.Value.BoolValue).FirstOrDefault() ?? false;
                });
        }

        private IDictionary<string, string> GetParameters(QueryResult queryResult)
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

        private Payload GetPayload(QueryResult s)
        {
            var payload = s?.FulfillmentMessages?
                            .Where(m => m.MessageCase == Intent.Types.Message.MessageOneofCase.Payload)
                            .Select(m => m.Payload.ToString().Deserialize<Payload>()).FirstOrDefault();

            return payload;
        }
    }
}
