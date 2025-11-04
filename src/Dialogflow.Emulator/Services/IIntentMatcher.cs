namespace Dialogflow.Emulator.Services;

using Dialogflow.Emulator.Models;

public interface IIntentMatcher
{
    Intent? Match(string text);
}
