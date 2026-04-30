namespace Dialogflow.Emulator.Services;

using System.Text.Json;
using Dialogflow.Emulator.Models;

public class AgentStorage(ILogger<AgentStorage> logger) : IAgentStorage
{
    private Dictionary<string, Intent> _intents = new();

    public async Task InitializeAsync(string agentPath)
    {
        var intentsPath = Path.Combine(agentPath, "intents");
        if (!Directory.Exists(intentsPath))
        {
            logger.LogWarning("Intents directory not found at {Path}", intentsPath);
            return;
        }

        var intentFiles = Directory.GetFiles(intentsPath, "*.json")
            .Where(file => !file.Contains("_usersays_"));

        foreach (var file in intentFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var intent = JsonSerializer.Deserialize<Intent>(json);
                if (intent != null && !string.IsNullOrEmpty(intent.Name))
                {
                    _intents[intent.Name] = intent;
                    logger.LogInformation("Loaded intent: {IntentName}", intent.Name);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load intent from {File}", file);
            }
        }
        logger.LogInformation("Total intents loaded: {Count}", _intents.Count);
    }

    public Intent? GetIntent(string name) => _intents.GetValueOrDefault(name);

    public Intent? FindIntentByEvent(string eventName) =>
        _intents.Values.FirstOrDefault(i =>
            i.Events?.Any(e => string.Equals(e.Name, eventName, StringComparison.OrdinalIgnoreCase)) ?? false);

    public IEnumerable<Intent> GetAllIntents() => _intents.Values;
}
