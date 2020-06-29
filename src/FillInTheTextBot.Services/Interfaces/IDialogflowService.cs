using System.Threading.Tasks;
using FillInTheTextBot.Models.Internal;

namespace FillInTheTextBot.Services
{
    public interface IDialogflowService
    {
        Task<Dialog> GetResponseAsync(Request request);

        Task<Dialog> GetResponseAsync(string text, string sessionId, string requiredContext = null);

        Task DeleteAllContexts(Request request);
    }
}