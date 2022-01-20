using System;
using AutoMapper;
using FillInTheTextBot.Models;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Buttons;
using Yandex.Dialogs.Models.Input;
using YandexModels = Yandex.Dialogs.Models;

namespace FillInTheTextBot.Messengers.Yandex
{
    /// <summary>
    /// Probably, registered at MappingRegistration of "Api" project
    /// </summary>
    public static class YandexMapping
    {
        public static Models.Request ToRequest(this InputModel source, Models.Request destinaton = null)
        {
            destinaton ??= new Models.Request();

            destinaton.ChatHash = source.Session?.SkillId;
            destinaton.UserHash = source.Session?.UserId;
            destinaton.Text = source.Request?.OriginalUtterance;
            destinaton.SessionId = source.Session?.SessionId;
            destinaton.NewSession = source.Session?.New;
            destinaton.Language = source.Meta?.Locale;
            destinaton.HasScreen = source?.Meta?.Interfaces?.Screen != null;
            destinaton.ClientId = source?.Meta?.ClientId;
            destinaton.Source = Source.Yandex;
            destinaton.Appeal = Appeal.NoOfficial;

            return destinaton;
        }

            //CreateMap<Models.Response, OutputModel>()
            //    .ForMember(d => d.Response, m => m.MapFrom(s => s))
            //    .ForMember(d => d.Session, m => m.MapFrom(s => s))
            //    .ForMember(d => d.Version, m => m.Ignore())
            //    .ForMember(d => d.StartAccountLinking, m => m.Ignore())
            //    .ForMember(d => d.UserStateUpdate, m => m.Ignore())
            //    .ForMember(d => d.SessionState, m => m.Ignore())
            //    .ForMember(d => d.ApplicationState, m => m.Ignore())
            //    .ForMember(d => d.Analytics, m => m.Ignore());

            //CreateMap<Models.Response, YandexModels.Response>()
            //    .ForMember(d => d.Text, m => m.MapFrom(s => s.Text.Replace(Environment.NewLine, "\n")))
            //    .ForMember(d => d.Tts, m => m.MapFrom(s => s.AlternativeText.Replace(Environment.NewLine, "\n")))
            //    .ForMember(d => d.EndSession, m => m.MapFrom(s => s.Finished))
            //    .ForMember(d => d.Buttons, m => m.MapFrom(s => s.Buttons))
            //    .ForMember(d => d.Card, m => m.Ignore())
            //    .ForMember(d => d.Directives, m => m.Ignore());

            //CreateMap<Models.Response, Session>()
            //    .ForMember(d => d.UserId, m => m.MapFrom(s => s.UserHash))
            //    .ForMember(d => d.MessageId, m => m.Ignore())
            //    .ForMember(d => d.SessionId, m => m.Ignore())
            //    .ForMember(d => d.Application, m => m.Ignore())
            //    .ForMember(d => d.User, m => m.Ignore());

            //CreateMap<InputModel, OutputModel>()
            //    .ForMember(d => d.Session, m => m.MapFrom(s => s.Session))
            //    .ForMember(d => d.Version, m => m.MapFrom(s => s.Version))
            //    .ForMember(d => d.Response, m => m.Ignore())
            //    .ForMember(d => d.StartAccountLinking, m => m.Ignore())
            //    .ForMember(d => d.UserStateUpdate, m => m.Ignore())
            //    .ForMember(d => d.SessionState, m => m.Ignore())
            //    .ForMember(d => d.ApplicationState, m => m.Ignore())
            //    .ForMember(d => d.Analytics, m => m.Ignore());

            //CreateMap<Models.Button, ResponseButton>()
            //    .ForMember(d => d.Title, m => m.MapFrom(s => s.Text))
            //    .ForMember(d => d.Url, m => m.MapFrom(s => !string.IsNullOrEmpty(s.Url) ? s.Url : null))
            //    .ForMember(d => d.Hide, m => m.MapFrom(s => s.IsQuickReply))
            //    .ForMember(d => d.Payload, m => m.Ignore());
    }
}
