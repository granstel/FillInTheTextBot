using System.Collections.Generic;
using System.Threading.Tasks;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services.Factories;

namespace FillInTheTextBot.Services;

/// <summary>
/// Прокси-сервис, который автоматически выбирает между Dialogflow и Rasa
/// на основе конфигурации, обеспечивая обратную совместимость
/// </summary>
public class NluServiceProxy : IDialogflowService
{
    private readonly INluServiceFactory _nluServiceFactory;

    public NluServiceProxy(INluServiceFactory nluServiceFactory)
    {
        _nluServiceFactory = nluServiceFactory;
    }

    public Task<Dialog> GetResponseAsync(Request request)
    {
        var service = _nluServiceFactory.CreateService();
        return service.GetResponseAsync(request);
    }

    public Task<Dialog> GetResponseAsync(string text, string sessionId, string scopeKey)
    {
        var service = _nluServiceFactory.CreateService();
        return service.GetResponseAsync(text, sessionId, scopeKey);
    }

    public Task SetContextAsync(string sessionId, string scopeKey, string contextName, int lifeSpan = 1,
        IDictionary<string, string> parameters = null)
    {
        var service = _nluServiceFactory.CreateService();
        return service.SetContextAsync(sessionId, scopeKey, contextName, lifeSpan, parameters);
    }
}