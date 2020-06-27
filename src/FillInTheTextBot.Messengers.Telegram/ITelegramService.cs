using System.Threading.Tasks;
using FillInTheTextBot.Services;
using Telegram.Bot.Types;

namespace FillInTheTextBot.Messengers.Telegram
{
    public interface ITelegramService : IMessengerService<Update, string>
    {
        Task<bool> TestApiAsync();

        Task<User> GetMeAsync();
    }
}