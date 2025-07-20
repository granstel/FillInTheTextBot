using System.Collections.Generic;
using System.Threading.Tasks;
using FillInTheTextBot.Models;

namespace FillInTheTextBot.Services.Interfaces;

/// <summary>
/// Общий интерфейс для NLU сервисов (Dialogflow, Rasa и др.)
/// </summary>
public interface INluService
{
    Task<Dialog> GetResponseAsync(Request request);

    Task<Dialog> GetResponseAsync(string text, string sessionId, string scopeKey);

    Task SetContextAsync(string sessionId, string scopeKey, string contextName, int lifeSpan = 1,
        IDictionary<string, string> parameters = null);
}