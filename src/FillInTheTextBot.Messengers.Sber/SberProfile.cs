using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Sber.SmartApp.Models;

namespace FillInTheTextBot.Messengers.Sber
{
    /// <summary>
    /// Probably, registered at MappingModule of "Services" project
    /// </summary>
    public class SberProfile : Profile
    {
        public SberProfile()
        {
            CreateMap<Request, Models.Request>()
                .ForMember(d => d.ChatHash, m => m.ResolveUsing(s => s?.Payload?.AppInfo?.ProjectId.ToString()))
                .ForMember(d => d.UserHash, m => m.ResolveUsing(s => s?.Uuid?.Sub ?? s?.Uuid?.UserId))
                .ForMember(d => d.Text, m => m.ResolveUsing(s => s?.Payload?.Message?.OriginalText))
                .ForMember(d => d.SessionId, m => m.ResolveUsing(s => s?.SessionId))
                .ForMember(d => d.NewSession, m => m.ResolveUsing(s => s?.Payload?.NewSession))
                .ForMember(d => d.Language, m => m.Ignore())
                .ForMember(d => d.HasScreen, m => m.ResolveUsing(s => s?.Payload?.Device?.Capabilities?.Screen?.Available ?? false))
                .ForMember(d => d.ClientId, m => m.ResolveUsing(s => s?.Payload?.Device?.Surface))
                .ForMember(d => d.Source, m => m.UseValue(Models.Source.Sber))
                .ForMember(d => d.RequiredContext, m => m.Ignore())
                .ForMember(d => d.ClearContexts, m => m.Ignore())
                .ForMember(d => d.IsOldUser, m => m.Ignore())
                .ForMember(d => d.NextTextIndex, m => m.Ignore())
                .ForMember(d => d.ScopeKey, m => m.Ignore());

            CreateMap<Models.Response, Response>()
                .ForMember(d => d.Payload, m => m.MapFrom(s => s))
                ;

            CreateMap<Models.Response, ResponsePayload>()
                .ForMember(d => d.PronounceText, m => m.MapFrom(s => s.Text))
                .ForMember(d => d.PronounceTextType, m => m.UseValue("application/text"))
                .ForMember(d => d.AutoListening, m => m.MapFrom(s => !s.Finished))
                .ForMember(d => d.Finished, m => m.MapFrom(s => s.Finished))
                .ForMember(d => d.Emotion, m => m.UseValue("igrivost"))
                .ForMember(d => d.Items, m => m.MapFrom(s => s.Buttons.Where(b => b.QuickReply)))
                .ForMember(d => d.Suggestions, m => m.MapFrom(s => s.Buttons.Where(b => !b.QuickReply)))
                ;

            CreateMap<string, Emotion>()
                .ForMember(d => d.EmotionId, m => m.MapFrom(s => s));

            CreateMap<Models.Button, Item>()
                .ForMember(d => d.Bubble, m => m.MapFrom(s => s));

            CreateMap<Models.Button, Bubble>()
                .ForMember(d => d.Text, m => m.MapFrom(s => s.Text));


            CreateMap<IEnumerable<Models.Button>, Suggestion>().ConvertUsing(MappingFunction);

            CreateMap<Models.Button, Button>()
                .ForMember(d => d.Title, m => m.MapFrom(s => s.Text))
                .ForMember(d => d.Action, m => m.MapFrom(s => s))
                ;

            CreateMap<Models.Button, Action>()
                .ForMember(d => d.Text, m => m.MapFrom(s => s.Text))
                .ForMember(d => d.DeepLink, m => m.MapFrom(s => s.Url))
                .ForMember(d => d.Type, m => m.ResolveUsing(s =>
                {
                    if(!string.IsNullOrEmpty(s.Url))
                    {
                        return "deep_link";
                    }

                    return "text";
                }));

            CreateMap<Request, Response>()
                .ForMember(d => d.MessageName, m => m.UseValue("ANSWER_TO_USER"))
                .ForMember(d => d.SessionId, m => m.MapFrom(s => s.SessionId))
                .ForMember(d => d.MessageId, m => m.MapFrom(s => s.MessageId))
                .ForMember(d => d.Uuid, m => m.MapFrom(s => s.Uuid))
                .ForMember(d => d.Payload, m => m.MapFrom(s => s.Payload))
                ;

            CreateMap<RequestPayload, ResponsePayload>()
                .ForMember(d => d.Device, m => m.MapFrom(s => s.Device))
                .ForMember(d => d.ProjectName, m => m.Ignore())
                .ForMember(d => d.Intent, m => m.Ignore())
                ;
        }

        private Suggestion MappingFunction(IEnumerable<Models.Button> source, Suggestion destination, ResolutionContext context)
        {
            var buttons = context.Mapper.Map<Button[]>(source);

            var suggest = new Suggestion
            {
                Buttons = buttons
            };

            return suggest;
        }
    }
}
