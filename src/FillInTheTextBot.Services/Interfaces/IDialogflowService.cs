using System.Threading.Tasks;
using FillInTheTextBot.Models.Internal;

namespace FillInTheTextBot.Services
{
    public interface IDialogflowService
    {
        Task<Dialog> GetResponseAsync(Request request);
    }
}