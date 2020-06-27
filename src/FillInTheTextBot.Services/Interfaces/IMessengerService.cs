using System.Threading.Tasks;

namespace FillInTheTextBot.Services
{
    public interface IMessengerService<TInput, TOutput>
    {
        Task<TOutput> ProcessIncomingAsync(TInput input);

        Task<bool> SetWebhookAsync(string url);

        Task<bool> DeleteWebhookAsync();
    }
}