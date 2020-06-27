using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;

namespace FillInTheTextBot.Messengers.Tests.Fixtures
{
    public class ControllerFixture : MessengerController<InputFixture, OutputFixture>
    {
        public ControllerFixture(IMessengerService<InputFixture, OutputFixture> messengerService, MessengerConfiguration configuration) : base(messengerService, configuration)
        {
        }
    }
}
