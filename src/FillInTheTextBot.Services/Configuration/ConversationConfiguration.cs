using System;

namespace FillInTheTextBot.Services.Configuration;

public class ConversationConfiguration
{
    public string[] ResetContextWords { get; set; } = Array.Empty<string>();
}