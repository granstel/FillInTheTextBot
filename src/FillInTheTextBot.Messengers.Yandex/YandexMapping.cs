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

        public static OutputModel ToOutput(this Models.Response source)
        {
            var destination = new OutputModel();

            destination.Response = source.ToResponse();
            destination.Session = source.ToSession();

            return destination;
        }

        public static YandexModels.Response ToResponse(this Models.Response source)
        {
            var destination = new YandexModels.Response();

            destination.Text = source?.Text?.Replace(Environment.NewLine, "\n");
            destination.Tts = source?.AlternativeText?.Replace(Environment.NewLine, "\n");
            destination.EndSession = source?.Finished ?? false;
            destination.Buttons = source?.Buttons?.ToResponseButtons();

            return destination;
        }

        public static Session ToSession(this Models.Response source)
        {
            var destination = new Session
            {
                UserId = source.UserHash
            };

            return destination;
        }

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
