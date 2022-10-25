using System.Collections.Generic;
using System.Threading.Tasks;
using FillInTheTextBot.Models;

namespace FillInTheTextBot.Services
{
    public interface IDialogflowService
    {
        Task<Dialog> GetResponseAsync(Request request);

        Task<Dialog> GetResponseAsync(string text, string sessionId, string requiredContext = null);

        Task SetContextAsync(string sessionId, string contextName, int lifeSpan = 1, IDictionary<string, string> parameters = null);
        Task DeleteAllContextsAsync(string sessionId);
    }
}