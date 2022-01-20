using System;
using System.Collections.Generic;
using FillInTheTextBot.Models;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Buttons;
using Yandex.Dialogs.Models.Input;
using YandexModels = Yandex.Dialogs.Models;

namespace FillInTheTextBot.Messengers.Yandex
{
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

        public static OutputModel ToOutput(this InputModel source, OutputModel destination)
        {
            destination.Session = source.Session;
            destination.Version = source.Version;

            return destination;
        }

        public static OutputModel ToOutput(this Models.Response s, OutputModel d = null)
        {
            d ??= new OutputModel();

            d.Response = s.ToResponse();
            //d.Session, m => m.MapFrom(s => s))

            return d;
        }

        public static YandexModels.Response ToResponse(this Models.Response s, YandexModels.Response d = null)
        {
            d ??= new YandexModels.Response();

            d.Text = s.Text.Replace(Environment.NewLine, "\n");
            d.Tts = s.AlternativeText.Replace(Environment.NewLine, "\n");
            d.EndSession = s.Finished;
            d.Buttons = s.Buttons.ToResponseButtons();

            return d;
        }

        //CreateMap<Models.Response, Session>()
        //    .ForMember(d => d.UserId, m => m.MapFrom(s => s.UserHash))
        //    .ForMember(d => d.MessageId, m => m.Ignore())
        //    .ForMember(d => d.SessionId, m => m.Ignore())
        //    .ForMember(d => d.Application, m => m.Ignore())
        //    .ForMember(d => d.User, m => m.Ignore());

        public static ResponseButton[] ToResponseButtons(this ICollection<Models.Button> source)
        {
            var responseButtons = new List<ResponseButton>();

            foreach (var button in source)
            {
                var responseButton = new ResponseButton();

                responseButton.Title = button.Text;
                responseButton.Url = !string.IsNullOrEmpty(button.Url) ? button.Url : null;
                responseButton.Hide = button.IsQuickReply;

                responseButtons.Add(responseButton);
            }

            return responseButtons.ToArray();
        }
    }
}
