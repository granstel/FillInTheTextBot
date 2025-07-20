namespace FillInTheTextBot.Services.Configuration;

public class NluConfiguration
{
    /// <summary>
    /// Провайдер NLU: Dialogflow или Rasa
    /// </summary>
    public NluProvider Provider { get; set; } = NluProvider.Dialogflow;
}

public enum NluProvider
{
    Dialogflow,
    Rasa
}