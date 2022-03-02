using System;
using System.Collections.Generic;
using FillInTheTextBot.Models;
using MailRu.Marusia.Models;
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
        public static Models.Request ToRequest(this InputModel source)
        {

            if (source == null) return null;

            var destinaton = new Models.Request();

            destinaton.ChatHash = source.Session?.SkillId;
            destinaton.UserHash = source.Session?.UserId;
            destinaton.Text = source.Request?.OriginalUtterance;
            destinaton.SessionId = source.Session?.SessionId;
            destinaton.NewSession = source.Session?.New;
            destinaton.Language = source.Meta?.Locale;
            destinaton.HasScreen = string.Equals(source?.Session?.Application?.ApplicationType, MarusiaModels.ApplicationTypes.Mobile);
            destinaton.ClientId = source?.Meta?.ClientId;
            destinaton.Source = Source.Marusia;
            destinaton.Appeal = Appeal.NoOfficial;

            return destinaton;
        }

        public static OutputModel ToOutput(this Models.Response source)
        {
            if (source == null) return null;

            var destination = new OutputModel();

            destination.Response = source.ToResponse();
            destination.Session = source.ToSession();

            return destination;
        }

        public static MarusiaModels.Response ToResponse(this Models.Response source)
        {
            if (source == null) return null;

            var destination = new MarusiaModels.Response();

            destination.Text = source.Text?.Replace(Environment.NewLine, "\n");
            destination.Tts = source.AlternativeText?.Replace(Environment.NewLine, "\n");
            destination.EndSession = source.Finished;
            destination.Buttons = source.Buttons?.ToResponseButtons();

            return destination;
        }

        public static ResponseButton[] ToResponseButtons(this ICollection<Models.Button> source)
        {
            if (source == null) return null;

            var responseButtons = new List<ResponseButton>();

            foreach (var button in source)
            {
                var responseButton = new ResponseButton();

                responseButton.Title = button?.Text;
                responseButton.Url = !string.IsNullOrEmpty(button?.Url) ? button?.Url : null;
                responseButton.Hide = button.IsQuickReply;

                responseButtons.Add(responseButton);
            }

            return responseButtons.ToArray();
        }

        public static Session ToSession(this Models.Response source)
        {
            if (source == null) return null;

            var destination = new Session
            {
                UserId = source.UserHash
            };

            return destination;
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
