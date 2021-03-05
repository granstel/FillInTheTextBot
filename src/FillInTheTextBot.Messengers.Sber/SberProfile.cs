using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using FillInTheTextBot.Services.Mapping;
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
                .ForMember(d => d.RequiredContexts, m => m.Ignore())
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
                .ForMember(d => d.Emotion, m => m.ResolveUsing(s =>
                {
                    s.Emotions.TryGetValue(EmotionsKeysMap.SourceEmotionKeys[Models.Source.Sber], out string emotionKey);

                    return emotionKey;
                }))
                .ForMember(d => d.Items, m => m.MapFrom(s => s))
                .ForMember(d => d.Suggestions, m => m.MapFrom(s => s.Buttons.Where(b => b.QuickReply)))
                ;

            CreateMap<string, Emotion>()
                .ForMember(d => d.EmotionId, m => m.MapFrom(s => s));

            CreateMap<Models.Response, PayloadItem[]>().ConvertUsing(MapResponseToItem);

            CreateMap<IEnumerable<Models.Button>, Suggestion>().ConvertUsing(MapButtonsToSuggestion);


            CreateMap<Models.Button, Button>()
                .ForMember(d => d.Title, m => m.MapFrom(s => s.Text))
                .ForMember(d => d.Action, m => m.MapFrom(s => s))
                ;

            CreateMap<Models.Button, Action>()
                .ForMember(d => d.Text, m => m.MapFrom(s => s.Text))
                .ForMember(d => d.DeepLink, m => m.MapFrom(s => s.Url))
                .ForMember(d => d.Type, m => m.ResolveUsing(s =>
                {
                    if (!string.IsNullOrEmpty(s.Url))
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

        private PayloadItem[] MapResponseToItem(Models.Response source, PayloadItem[] destinations, ResolutionContext context)
        {
            var item = new PayloadItem
            {
                Bubble = { Text = source.Text }
            };

            var buttons = source.Buttons.Where(b => !b.QuickReply).ToList();

            var items = buttons.Select(b =>
            {
                var cardItem = new CardItem
                {
                    Type = "greeting_grid_item",
                    TopText = new CardItemText
                    {
                        Type = "text_cell_view",
                        Text = string.Empty,
                        Typeface = "caption",
                        TextColor = "default"
                    },
                    BottomText = new CardItemText
                    {
                        Type = "left_right_cell_view",
                        Text = b.Text,
                        Typeface = "button1",
                        TextColor = "default",
                        MaxLines = 2,
                        Margins = new Margins
                        {
                            Top = "0x",
                        }
                    },
                    Paddings = new Paddings
                    {
                        Top = "0x",
                        Left = "6x",
                        Right = "6x",
                        Bottom = "16x"
                    }
                };

                var action = context.Mapper.Map<Action>(b);

                cardItem.Actions = new[] { action };

                return cardItem;
            }).ToArray();

            var card = new Card
            {
                Type = "grid_card",
                Items = items,
                Columns = 2,
                ItemWidth = "resizable"
            };

            item.Card = card;

            return new[] { item };
        }

        private Suggestion MapButtonsToSuggestion(IEnumerable<Models.Button> source, Suggestion destination, ResolutionContext context)
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
