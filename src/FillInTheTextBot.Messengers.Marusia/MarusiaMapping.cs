using System;
using System.Collections.Generic;
using FillInTheTextBot.Models;
using MailRu.Marusia.Models.Buttons;
using MailRu.Marusia.Models.Input;
using Button = FillInTheTextBot.Models.Button;
using MarusiaModels = MailRu.Marusia.Models;

namespace FillInTheTextBot.Messengers.Marusia;

public static class MarusiaMapping
{
    public static Request ToRequest(this InputModel source)
    {
        if (source == null) return null;

        var destinaton = new Request();

        destinaton.ChatHash = source.Session?.SkillId;
        destinaton.UserHash = source.Session?.UserId;
        destinaton.Text = source.Request?.OriginalUtterance;
        destinaton.SessionId = source.Session?.SessionId;
        destinaton.NewSession = source.Session?.New;
        destinaton.Language = source.Meta?.Locale;
        destinaton.HasScreen = string.Equals(source?.Session?.Application?.ApplicationType,
            MarusiaModels.ApplicationTypes.Mobile);
        destinaton.ClientId = source?.Meta?.ClientId;
        destinaton.Source = Source.Marusia;
        destinaton.Appeal = Appeal.NoOfficial;

        return destinaton;
    }

    public static MarusiaModels.OutputModel FillOutput(this InputModel source, MarusiaModels.OutputModel destination)
    {
        if (source == null) return null;
        if (destination == null) return null;

        destination.Session = source.Session;
        destination.Version = source.Version;

        return destination;
    }

    public static MarusiaModels.OutputModel ToOutput(this Response source)
    {
        if (source == null) return null;

        var destination = new MarusiaModels.OutputModel();

        destination.Response = source.ToResponse();
        destination.Session = source.ToSession();

        return destination;
    }

    public static MarusiaModels.Response ToResponse(this Response source)
    {
        if (source == null) return null;

        var destination = new MarusiaModels.Response();

        destination.Text = source.Text?.Replace(Environment.NewLine, "\n");
        destination.Tts = source.AlternativeText?.Replace(Environment.NewLine, "\n");
        destination.EndSession = source.Finished;
        destination.Buttons = source.Buttons?.ToResponseButtons();

        return destination;
    }

    public static MarusiaModels.Session ToSession(this Response source)
    {
        if (source == null) return null;

        var destination = new MarusiaModels.Session
        {
            UserId = source.UserHash
        };

        return destination;
    }

    public static ResponseButton[] ToResponseButtons(this ICollection<Button> source)
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
}