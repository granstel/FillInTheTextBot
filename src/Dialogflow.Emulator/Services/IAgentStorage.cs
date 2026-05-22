namespace Dialogflow.Emulator.Services;

using Dialogflow.Emulator.Models;

public interface IAgentStorage
{
    Task InitializeAsync(string agentPath);
    Intent? GetIntent(string name);
    Intent? FindIntentByEvent(string eventName);
    IEnumerable<Intent> GetAllIntents();
}
