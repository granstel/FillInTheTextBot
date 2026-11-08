using MailRu.Marusia.Models;
using MailRu.Marusia.Models.Input;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Messengers.Marusia
{
    public class MarusiaController : MessengerController<InputModel, OutputModel>
    {
        public MarusiaController(ILogger<MarusiaController> log, IMarusiaService marusiaService, MarusiaConfiguration configuration)
            : base(log, marusiaService, configuration)
        {
        }
    }
}
