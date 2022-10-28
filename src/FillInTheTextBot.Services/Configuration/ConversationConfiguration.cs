using System;

namespace FillInTheTextBot.Services.Configuration;

public class ConversationConfiguration
{
    public ConversationConfiguration()
    {
        ResetContextWords = Array.Empty<string>();
    }

    public string[] ResetContextWords { get; set; }
}