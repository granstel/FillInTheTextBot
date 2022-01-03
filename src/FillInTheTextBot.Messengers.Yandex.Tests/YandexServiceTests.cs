using System.Threading.Tasks;
using AutoFixture;
using AutoMapper;
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
        private Mock<IMapper> _mapper;

        private YandexService _target;

        private Fixture _fixture;

        [SetUp]
        public void InitTest()
        {
            _mockRepository = new MockRepository(MockBehavior.Strict);

            _conversationService = _mockRepository.Create<IConversationService>();
            _mapper = _mockRepository.Create<IMapper>();
            var log = Mock.Of<ILogger<YandexService>>();

            _target = new YandexService(log, _conversationService.Object, _mapper.Object);

            _fixture = new Fixture();
        }

        [Test]
        public async Task ProcessIncomingAsync_Invokations_Success()
        {
            var inputModel = _fixture.Build<InputModel>()
                .OmitAutoProperties()
                .Create();

            var request = _fixture.Build<Models.Request>()
                .OmitAutoProperties()
                .Create();

            _mapper.Setup(m => m.Map<Models.Request>(It.IsAny<InputModel>())).Returns(request);

            _conversationService.Setup(s => s.GetResponseAsync(request)).ReturnsAsync(() => new Models.Response());

            _mapper.Setup(m => m.Map(It.IsAny<Models.Request>(), It.IsAny<Models.Response>())).Returns(() => null);

            var output = _fixture.Build<OutputModel>()
                .With(o => o.Session)
                .OmitAutoProperties()
                .Create();

            _mapper.Setup(m => m.Map<OutputModel>(It.IsAny<Models.Response>())).Returns(output);
            _mapper.Setup(m => m.Map(It.IsAny<InputModel>(), It.IsAny<OutputModel>())).Returns(() => null);


            var result = await _target.ProcessIncomingAsync(inputModel);


            _mockRepository.VerifyAll();

            Assert.NotNull(result);
        }
    }
}
