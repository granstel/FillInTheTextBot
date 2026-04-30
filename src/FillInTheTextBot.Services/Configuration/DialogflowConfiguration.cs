namespace FillInTheTextBot.Services.Configuration;

public class DialogflowConfiguration
{
    public virtual string ScopeId { get; set; }

    public virtual string ProjectId { get; set; }

    public virtual string JsonPath { get; set; }

    public virtual string Region { get; set; }

    public virtual string LanguageCode => "ru";

    public bool LogQuery { get; set; }

    public bool DoNotUseForNewSessions { get; set; }
    
    /// <summary>
    /// Endpoint для эмулятора Dialogflow (например, "localhost:3000" для локальной разработки)
    /// </summary>
    public string EmulatorEndpoint { get; set; }
}