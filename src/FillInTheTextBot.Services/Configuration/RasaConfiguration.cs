namespace FillInTheTextBot.Services.Configuration;

public class RasaConfiguration
{
    public virtual string ScopeId { get; set; }

    public virtual string BaseUrl { get; set; } = "http://localhost:5005";

    public virtual string LanguageCode => "ru";

    public bool LogQuery { get; set; }

    public bool DoNotUseForNewSessions { get; set; }
}