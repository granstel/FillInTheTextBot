using Microsoft.Extensions.Logging;
using Sber.SmartApp.Models;

namespace FillInTheTextBot.Messengers.Sber
{
    public class SberController : MessengerController<Request, Response>
    {
        public SberController(ILogger<SberController> log, ISberService sberService, SberConfiguration configuration)
            : base(log, sberService, configuration)
        {
        }
    }
}
