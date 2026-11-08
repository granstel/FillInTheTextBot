using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Input;

namespace FillInTheTextBot.Messengers.Yandex
{
    public class YandexController : MessengerController<InputModel, OutputModel>
    {
        public YandexController(ILogger<YandexController> log, IYandexService yandexService, YandexConfiguration configuration)
            : base(log, yandexService, configuration)
        {
        }

        [NonAction]
        public override Task<IActionResult> WebHook(InputModel input, string token)
        {
            return base.WebHook(input, token);
        }
    }
}
