using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Input;

namespace FillInTheTextBot.Messengers.Yandex
{
    [Produces("application/json")]
    public class YandexController : MessengerController<InputModel, OutputModel>
    {
        public YandexController(ILogger<YandexController> log, IYandexService yandexService, YandexConfiguration configuration)
            : base(log, yandexService, configuration)
        {
        }
    }
}
