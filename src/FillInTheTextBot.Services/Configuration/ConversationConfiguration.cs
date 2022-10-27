using System;

namespace FillInTheTextBot.Services.Configuration;

public class ConversationConfiguration
{
    public ConversationConfiguration()
    {
        HelpWords = Array.Empty<string>();
        ExitWords = Array.Empty<string>();
    }

    public string[] HelpWords { get; set; }

    public string[] ExitWords { get; set; }
}