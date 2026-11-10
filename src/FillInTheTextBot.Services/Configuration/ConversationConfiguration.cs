using System;

namespace FillInTheTextBot.Services.Configuration;

public class ConversationConfiguration
{
    public ConversationProvider Provider { get; set; } = ConversationProvider.Dialogflow;

    public ConversationConfiguration()
    {
        ResetContextWords = Array.Empty<string>();
    }

    public string[] ResetContextWords { get; set; }
}
