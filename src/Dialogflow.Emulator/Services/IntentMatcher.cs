namespace Dialogflow.Emulator.Services;

using Dialogflow.Emulator.Models;

public class IntentMatcher(IAgentStorage agentStorage) : IIntentMatcher
{
    private readonly Dictionary<string, string[]> _keywordMap = new()
    {
        { "Default Welcome Intent", ["привет", "начать", "hello", "/start"] },
        { "EasyWelcome", ["да", "конечно", "давай"] },
        { "Exit", ["выход", "выйти", "стоп", "пока"] },
        { "Help", ["помощь", "что ты умеешь", "справка"] },
        { "TextsList", ["список текстов", "список историй", "тексты"] },
        { "Yes", ["да", "ага", "конечно", "угу"] },
        { "No", ["нет", "не хочу", "не буду"] }
    };

    public Intent? Match(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return GetFallbackIntent();

        var lowerText = text.ToLowerInvariant().Trim();

        foreach (var (intentName, keywords) in _keywordMap)
        {
            if (keywords.Any(keyword => lowerText.Contains(keyword)))
            {
                var intent = agentStorage.GetIntent(intentName);
                if (intent != null)
                    return intent;
            }
        }

        return GetFallbackIntent();
    }

    private Intent? GetFallbackIntent() => agentStorage.GetIntent("Default Fallback Intent");
}
