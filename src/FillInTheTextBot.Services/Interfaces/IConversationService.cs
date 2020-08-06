using System.Threading.Tasks;
using FillInTheTextBot.Models;

namespace FillInTheTextBot.Services
{
    public interface IConversationService
    {
        Task<Response> GetResponseAsync(Request request);
    }
}