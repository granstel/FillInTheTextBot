using FillInTheTextBot.Services;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Input;

namespace FillInTheTextBot.Messengers.Yandex
{
    public interface IYandexService : IMessengerService<InputModel, OutputModel>
    {
    }
}