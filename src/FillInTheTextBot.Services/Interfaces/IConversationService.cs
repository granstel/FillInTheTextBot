using System.Threading.Tasks;
using FillInTheTextBot.Models.Internal;

namespace FillInTheTextBot.Services
{
    public interface IConversationService
    {
        Task<Response> GetResponseAsync(Request request);
    }
}