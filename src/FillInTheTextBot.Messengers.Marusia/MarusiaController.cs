using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Input;

namespace FillInTheTextBot.Messengers.Marusia
{
    [Produces("application/json")]
    public class MarusiaController : MessengerController<InputModel, OutputModel>
    {
        public MarusiaController(IMarusiaService marusiaService, MarusiaConfiguration configuration) : base(marusiaService, configuration)
        {
            SerializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };
        }
    }
}
