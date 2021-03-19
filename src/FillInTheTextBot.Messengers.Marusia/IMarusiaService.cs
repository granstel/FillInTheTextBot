using FillInTheTextBot.Services;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Input;

namespace FillInTheTextBot.Messengers.Marusia
{
    public interface IMarusiaService : IMessengerService<InputModel, OutputModel>
    {
    }
}