using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Input;

namespace FillInTheTextBot.Messengers.Yandex
{
    public class YandexController : MessengerController<InputModel, OutputModel>
    {
        public YandexController(ILogger<YandexController> log, IYandexService yandexService, YandexConfiguration configuration)
            : base(log, yandexService, configuration)
        {
            SerializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };
        }
    }
}
