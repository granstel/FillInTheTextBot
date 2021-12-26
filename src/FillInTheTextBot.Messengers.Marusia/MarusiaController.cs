using MailRu.Marusia.Models;
using MailRu.Marusia.Models.Input;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FillInTheTextBot.Messengers.Marusia
{
    [Produces("application/json")]
    public class MarusiaController : MessengerController<InputModel, OutputModel>
    {
        public MarusiaController(ILogger<MarusiaController> log, IMarusiaService marusiaService, MarusiaConfiguration configuration)
            : base(log, marusiaService, configuration)
        {
            SerializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };
        }
    }
}
