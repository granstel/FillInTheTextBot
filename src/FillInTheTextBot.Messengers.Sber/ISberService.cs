using FillInTheTextBot.Services;
using Sber.SmartApp.Models;

namespace FillInTheTextBot.Messengers.Sber
{
    public interface ISberService : IMessengerService<Request, Response>
    {
    }
}