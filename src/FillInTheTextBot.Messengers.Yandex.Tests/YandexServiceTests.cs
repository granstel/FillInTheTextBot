using System.Threading.Tasks;
using AutoFixture;
using FillInTheTextBot.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Input;

namespace FillInTheTextBot.Messengers.Yandex.Tests
{
    [TestFixture]
    public class YandexServiceTests
    {
        private MockRepository _mockRepository;

        private Mock<IConversationService> _conversationService;

        private YandexService _target;

        private Fixture _fixture;

        [SetUp]
        public void InitTest()
        {
            _mockRepository = new MockRepository(MockBehavior.Strict);

            _conversationService = _mockRepository.Create<IConversationService>();
            var log = Mock.Of<ILogger<YandexService>>();

            _target = new YandexService(log, _conversationService.Object);

            _fixture = new Fixture();
        }

        [Test]
        public async Task ProcessIncomingAsync_Invokations_Success()
        {
            var inputModel = _fixture.Build<InputModel>()
                .OmitAutoProperties()
                .Create();

            _conversationService.Setup(s => s.GetResponseAsync(It.IsAny<Models.Request>())).ReturnsAsync(() => new Models.Response());

            var output = _fixture.Build<OutputModel>()
                .With(o => o.Session)
                .OmitAutoProperties()
                .Create();


            var result = await _target.ProcessIncomingAsync(inputModel);


            _mockRepository.VerifyAll();

            Assert.That(result, Is.Not.Null);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task ProcessIncomingAsync_RotationStateInUserOrApplicationState_AppliedToRequest(bool inUserState)
        {
            const string rotationState = "{\"v\":2}";

            var input = CreateInput();
            var state = inUserState ? input.State.User : input.State.Application;
            state.Add(Models.Response.RotationStateStorageKey, rotationState);

            Models.Request captured = null;

            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<Models.Request>()))
                .Callback<Models.Request>(r => captured = r)
                .ReturnsAsync(new Models.Response());


            await _target.ProcessIncomingAsync(input);


            Assert.That(captured.RotationState, Is.EqualTo(rotationState));
        }

        [Test]
        public async Task ProcessIncomingAsync_RotationState_WrittenToUserAndApplicationState()
        {
            var response = new Models.Response { RotationState = "{\"v\":2}" };

            _conversationService.Setup(s => s.GetResponseAsync(It.IsAny<Models.Request>())).ReturnsAsync(response);


            var result = await _target.ProcessIncomingAsync(CreateInput());


            Assert.That(result.UserStateUpdate[Models.Response.RotationStateStorageKey], Is.EqualTo(response.RotationState));
            Assert.That(result.ApplicationState[Models.Response.RotationStateStorageKey], Is.EqualTo(response.RotationState));
        }

        private static InputModel CreateInput()
        {
            return new InputModel
            {
                Session = new InputSession(),
                Request = new Request { OriginalUtterance = "давай" },
                State = new InputState { User = new State(), Application = new State(), Session = new State() }
            };
        }
    }
}
