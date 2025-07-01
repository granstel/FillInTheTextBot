using System.Threading.Tasks;
using AutoFixture;
using FillInTheTextBot.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Input;
using Request = FillInTheTextBot.Models.Request;
using Response = FillInTheTextBot.Models.Response;

namespace FillInTheTextBot.Messengers.Yandex.Tests;

[TestFixture]
public class YandexServiceTests
{
    [SetUp]
    public void InitTest()
    {
        _mockRepository = new MockRepository(MockBehavior.Strict);

        _conversationService = _mockRepository.Create<IConversationService>();
        var log = Mock.Of<ILogger<YandexService>>();

        _target = new YandexService(log, _conversationService.Object);

        _fixture = new Fixture();
    }

    private MockRepository _mockRepository;

    private Mock<IConversationService> _conversationService;

    private YandexService _target;

    private Fixture _fixture;

    [Test]
    public async Task ProcessIncomingAsync_Invokations_Success()
    {
        var inputModel = _fixture.Build<InputModel>()
            .OmitAutoProperties()
            .Create();

        _conversationService.Setup(s => s.GetResponseAsync(It.IsAny<Request>())).ReturnsAsync(() => new Response());

        var output = _fixture.Build<OutputModel>()
            .With(o => o.Session)
            .OmitAutoProperties()
            .Create();


        var result = await _target.ProcessIncomingAsync(inputModel);


        _mockRepository.VerifyAll();

        Assert.NotNull(result);
    }
}