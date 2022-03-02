using System;
using AutoMapper;
using FillInTheTextBot.Models;
using MailRu.Marusia.Models.Buttons;
using MailRu.Marusia.Models.Input;
using MarusiaModels = MailRu.Marusia.Models;

namespace FillInTheTextBot.Messengers.Marusia
{
    /// <summary>
    /// Probably, registered at MappingRegistration of "Api" project
    /// </summary>
    public static class MarusiaMapping
    {
        public static Request ToRequest(this InputModel s)
        {

            if (s == null) return null;

            var destinaton = new Request();

            destinaton.ChatHash = s.Session?.SkillId;
            destinaton.UserHash = s.Session?.UserId;
            destinaton.Text = s.Request?.OriginalUtterance;
            destinaton.SessionId = s.Session?.SessionId;
            destinaton.NewSession = s.Session?.New;
            destinaton.Language = s.Meta?.Locale;
            destinaton.HasScreen = string.Equals(s?.Session?.Application?.ApplicationType, MarusiaModels.ApplicationTypes.Mobile);
            destinaton.ClientId = s?.Meta?.ClientId;
            destinaton.Source = Source.Marusia;
            destinaton.Appeal = Appeal.NoOfficial;

            return destinaton;
        }
            
        //public MarusiaMapping()
        //{
        //    CreateMap<Response, MarusiaModels.OutputModel>()
        //        .ForMember(d => d.Response, m => m.MapFrom(s => s))
        //        .ForMember(d => d.Session, m => m.MapFrom(s => s))
        //        .ForMember(d => d.Version, m => m.Ignore())
        //        .ForMember(d => d.UserStateUpdate, m => m.Ignore())
        //        .ForMember(d => d.SessionState, m => m.Ignore());

        //    CreateMap<Response, MarusiaModels.Response>()
        //        .ForMember(d => d.Text, m => m.MapFrom(s => s.Text.Replace(Environment.NewLine, "\n")))
        //        .ForMember(d => d.Tts, m => m.MapFrom(s => s.AlternativeText.Replace(Environment.NewLine, "\n")))
        //        .ForMember(d => d.EndSession, m => m.MapFrom(s => s.Finished))
        //        .ForMember(d => d.Buttons, m => m.MapFrom(s => s.Buttons))
        //        .ForMember(d => d.Card, m => m.Ignore());

        //    CreateMap<Response, MarusiaModels.Session>()
        //        .ForMember(d => d.UserId, m => m.MapFrom(s => s.UserHash))
        //        .ForMember(d => d.MessageId, m => m.Ignore())
        //        .ForMember(d => d.SessionId, m => m.Ignore())
        //        .ForMember(d => d.Application, m => m.Ignore())
        //        .ForMember(d => d.User, m => m.Ignore());

        //    CreateMap<InputModel, MarusiaModels.OutputModel>()
        //        .ForMember(d => d.Session, m => m.MapFrom(s => s.Session))
        //        .ForMember(d => d.Version, m => m.MapFrom(s => s.Version))
        //        .ForMember(d => d.Response, m => m.Ignore())
        //        .ForMember(d => d.UserStateUpdate, m => m.Ignore())
        //        .ForMember(d => d.SessionState, m => m.Ignore());

        //    CreateMap<Models.Button, ResponseButton>()
        //        .ForMember(d => d.Title, m => m.MapFrom(s => s.Text))
        //        .ForMember(d => d.Url, m => m.MapFrom(s => !string.IsNullOrEmpty(s.Url) ? s.Url : null))
        //        .ForMember(d => d.Hide, m => m.MapFrom(s => s.IsQuickReply))
        //        .ForMember(d => d.Payload, m => m.Ignore());
        //}
    }
}
