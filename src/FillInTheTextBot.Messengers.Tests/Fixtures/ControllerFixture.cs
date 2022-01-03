using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Messengers.Tests.Fixtures
{
    public class ControllerFixture : MessengerController<InputFixture, OutputFixture>
    {
        public ControllerFixture(ILogger<ControllerFixture> log, IMessengerService<InputFixture, OutputFixture> messengerService, MessengerConfiguration configuration)
            : base(log, messengerService, configuration)
        {
        }
    }
}
