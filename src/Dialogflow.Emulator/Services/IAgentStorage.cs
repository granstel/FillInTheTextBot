using Dialogflow.Emulator.Models;

namespace Dialogflow.Emulator.Services;

public interface IAgentStorage
{
    Task InitializeAsync(string agentPath);

    AgentIntent? FindByEvent(string eventName);

    AgentIntent? FindByText(string text);

    AgentIntent? GetFallback();
}
