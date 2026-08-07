using System;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using FillInTheTextBot.Messengers.Tests.Fixtures;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace FillInTheTextBot.Messengers.Tests
{
    /// <summary>
    /// Характеризующие тесты: фиксируют текущее поведение базового
    /// <see cref="MessengerService{TInput,TOutput}"/> перед обновлением
    /// фреймворка и пакетов.
    /// </summary>
    [TestFixture]
    public class MessengerServiceTests
    {
        private const string ErrorAnswerStart = "Прости, у меня какие-то проблемы...";
        private const string ErrorLink = "https://vk.com/fillinthetextbot";

        private Mock<IConversationService> _conversationService;

        private MessengerServiceFixture _target;

        private Fixture _fixture;

        [SetUp]
        public void InitTest()
        {
            _conversationService = new Mock<IConversationService>();

            var log = Mock.Of<ILogger>();

            _target = new MessengerServiceFixture(log, _conversationService.Object);

            _fixture = new Fixture { OmitAutoProperties = true };
        }

        #region Основной поток

        [Test]
        public async Task ProcessIncomingAsync_HappyPath_BeforeConversationAfterInvoked()
        {
            var request = new Request();
            var response = new Response { Text = _fixture.Create<string>() };

            _target.BeforeResult = request;
            _conversationService.Setup(s => s.GetResponseAsync(request)).ReturnsAsync(response);


            var result = await _target.ProcessIncomingAsync(new InputFixture());


            Assert.AreSame(request, _target.AfterInput.Request);
            Assert.AreSame(response, _target.AfterInput.Response);
            Assert.NotNull(result);
        }

        [Test]
        public async Task ProcessIncomingAsync_ProcessCommandReturnsResponse_ConversationNotCalled()
        {
            var commandResponse = new Response { Text = _fixture.Create<string>() };

            _target.BeforeResult = new Request();
            _target.ProcessCommandResult = commandResponse;


            await _target.ProcessIncomingAsync(new InputFixture());


            _conversationService.Verify(s => s.GetResponseAsync(It.IsAny<Request>()), Times.Never);
            Assert.AreSame(commandResponse, _target.AfterInput.Response);
        }

        #endregion Основной поток

        #region Контексты

        [Test]
        public async Task ProcessIncomingAsync_Always_AddsSourceContextWithUserHashAndClientId()
        {
            var request = new Request
            {
                Source = Source.Sber,
                UserHash = _fixture.Create<string>(),
                ClientId = _fixture.Create<string>()
            };

            _target.BeforeResult = request;
            _conversationService.Setup(s => s.GetResponseAsync(It.IsAny<Request>())).ReturnsAsync(new Response());


            await _target.ProcessIncomingAsync(new InputFixture());


            var context = request.RequiredContexts.Single(c => c.Name == "source-Sber");

            Assert.AreEqual(2, context.LifeSpan);
            Assert.AreEqual(request.UserHash, context.Parameters[nameof(request.UserHash)]);
            Assert.AreEqual(request.ClientId, context.Parameters[nameof(request.ClientId)]);
        }

        [Test]
        public async Task ProcessIncomingAsync_NullUserHashAndClientId_EmptyStringsInParameters()
        {
            var request = new Request { Source = Source.Yandex };

            _target.BeforeResult = request;
            _conversationService.Setup(s => s.GetResponseAsync(It.IsAny<Request>())).ReturnsAsync(new Response());


            await _target.ProcessIncomingAsync(new InputFixture());


            var context = request.RequiredContexts.Single(c => c.Name == "source-Yandex");

            Assert.AreEqual(string.Empty, context.Parameters[nameof(request.UserHash)]);
            Assert.AreEqual(string.Empty, context.Parameters[nameof(request.ClientId)]);
        }

        [Test]
        public async Task ProcessIncomingAsync_HasScreen_ScreenContextAdded()
        {
            var request = new Request { HasScreen = true };

            _target.BeforeResult = request;
            _conversationService.Setup(s => s.GetResponseAsync(It.IsAny<Request>())).ReturnsAsync(new Response());


            await _target.ProcessIncomingAsync(new InputFixture());


            var context = request.RequiredContexts.Single(c => c.Name == "screen");

            Assert.AreEqual(2, context.LifeSpan);
        }

        [Test]
        public async Task ProcessIncomingAsync_IsOldUser_OldUserContextAdded()
        {
            var request = new Request { IsOldUser = true };

            _target.BeforeResult = request;
            _conversationService.Setup(s => s.GetResponseAsync(It.IsAny<Request>())).ReturnsAsync(new Response());


            await _target.ProcessIncomingAsync(new InputFixture());


            var context = request.RequiredContexts.Single(c => c.Name == "OldUser");

            Assert.AreEqual(2, context.LifeSpan);
        }

        [Test]
        public async Task ProcessIncomingAsync_NewUserWithoutScreen_OnlySourceContext()
        {
            var request = new Request();

            _target.BeforeResult = request;
            _conversationService.Setup(s => s.GetResponseAsync(It.IsAny<Request>())).ReturnsAsync(new Response());


            await _target.ProcessIncomingAsync(new InputFixture());


            Assert.AreEqual(1, request.RequiredContexts.Count);
        }

        #endregion Контексты

        #region Обработка ошибок

        [Test]
        public async Task ProcessIncomingAsync_BeforeThrows_ErrorAnswer()
        {
            _target.BeforeException = new InvalidOperationException(_fixture.Create<string>());


            await _target.ProcessIncomingAsync(new InputFixture());


            var response = _target.AfterInput.Response;

            Assert.True(response.Text.StartsWith(ErrorAnswerStart));
            Assert.AreEqual(ErrorLink, response.Buttons.Single().Url);
        }

        [Test]
        public async Task ProcessIncomingAsync_ConversationThrows_ErrorAnswer()
        {
            _target.BeforeResult = new Request();

            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<Request>()))
                .ThrowsAsync(new InvalidOperationException(_fixture.Create<string>()));


            await _target.ProcessIncomingAsync(new InputFixture());


            Assert.True(_target.AfterInput.Response.Text.StartsWith(ErrorAnswerStart));
        }

        [Test]
        public async Task ProcessIncomingAsync_AnyOutcome_AfterAsyncAlwaysInvoked()
        {
            _target.BeforeException = new InvalidOperationException(_fixture.Create<string>());


            var result = await _target.ProcessIncomingAsync(new InputFixture());


            Assert.NotNull(result);
            Assert.NotNull(_target.AfterInput);
        }

        #endregion Обработка ошибок

        #region Не реализованные по умолчанию методы

        [Test]
        public void ProcessIncomingAsync_BeforeAndAfterNotOverridden_NotImplemented()
        {
            // Before бросает NotImplementedException, она перехватывается и превращается
            // в ответ об ошибке, но AfterAsync вызывается вне try и падает уже наружу
            var target = new NotOverriddenMessengerService(Mock.Of<ILogger>(), _conversationService.Object);

            Assert.ThrowsAsync<NotImplementedException>(() => target.ProcessIncomingAsync(new InputFixture()));
        }

        [Test]
        public void SetWebhookAsync_NotOverridden_NotImplemented()
        {
            Assert.ThrowsAsync<NotImplementedException>(() => _target.SetWebhookAsync(_fixture.Create<string>()));
        }

        [Test]
        public void DeleteWebhookAsync_NotOverridden_NotImplemented()
        {
            Assert.ThrowsAsync<NotImplementedException>(() => _target.DeleteWebhookAsync());
        }

        #endregion Не реализованные по умолчанию методы

        private class AfterCall
        {
            public Request Request { get; set; }

            public Response Response { get; set; }
        }

        private class MessengerServiceFixture : MessengerService<InputFixture, OutputFixture>
        {
            public MessengerServiceFixture(ILogger log, IConversationService conversationService)
                : base(log, conversationService)
            {
            }

            public Request BeforeResult { get; set; }

            public Exception BeforeException { get; set; }

            public Response ProcessCommandResult { get; set; }

            public AfterCall AfterInput { get; private set; }

            protected override Request Before(InputFixture input)
            {
                if (BeforeException != null)
                {
                    throw BeforeException;
                }

                return BeforeResult;
            }

            protected override Response ProcessCommand(Request request)
            {
                return ProcessCommandResult;
            }

            protected override Task<OutputFixture> AfterAsync(InputFixture input, Response response)
            {
                AfterInput = new AfterCall { Request = BeforeResult, Response = response };

                return Task.FromResult(new OutputFixture());
            }
        }

        private class NotOverriddenMessengerService : MessengerService<InputFixture, OutputFixture>
        {
            public NotOverriddenMessengerService(ILogger log, IConversationService conversationService)
                : base(log, conversationService)
            {
            }
        }
    }
}
