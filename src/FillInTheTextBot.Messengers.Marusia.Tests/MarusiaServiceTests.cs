using System;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.BackgroundTasks;
using GranSteL.Helpers.Redis;
using MailRu.Marusia.Models;
using MailRu.Marusia.Models.Input;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using InternalModels = FillInTheTextBot.Models;

namespace FillInTheTextBot.Messengers.Marusia.Tests
{
    /// <summary>
    /// Характеризующие тесты: фиксируют текущее поведение <see cref="MarusiaService"/>
    /// перед обновлением фреймворка и пакетов.
    /// </summary>
    [TestFixture]
    public class MarusiaServiceTests
    {
        private Mock<IConversationService> _conversationService;
        private Mock<IRedisCacheService> _cache;
        private Mock<IBackgroundTaskQueue> _backgroundTasks;

        private MarusiaService _target;

        private Fixture _fixture;

        [SetUp]
        public void InitTest()
        {
            _conversationService = new Mock<IConversationService>();
            _cache = new Mock<IRedisCacheService>();

            _backgroundTasks = new Mock<IBackgroundTaskQueue>();

            // Фоновые работы выполняются сразу, чтобы тест проверял результат, а не гонку
            _backgroundTasks
                .Setup(q => q.Enqueue(It.IsAny<string>(), It.IsAny<Func<Task>>()))
                .Returns((string _, Func<Task> work) =>
                {
                    work().GetAwaiter().GetResult();
                    return true;
                });

            _target = new MarusiaService(Mock.Of<ILogger<MarusiaService>>(), _conversationService.Object, _cache.Object, _backgroundTasks.Object);

            _fixture = new Fixture { OmitAutoProperties = true };
        }

        private InputModel CreateInput(string command = null)
        {
            return new InputModel
            {
                Session = new InputSession { UserId = _fixture.Create<string>() },
                Request = new Request { OriginalUtterance = command ?? _fixture.Create<string>() },
                State = new InputState { User = new State(), Session = new State() }
            };
        }

        private InternalModels.Request SetupConversation(InternalModels.Response response = null)
        {
            InternalModels.Request captured = null;

            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<InternalModels.Request>()))
                .Callback<InternalModels.Request>(r => captured = r)
                .ReturnsAsync(response ?? new InternalModels.Response());

            return captured;
        }

        #region Before: состояние пользователя и сессии

        [Test]
        public async Task ProcessIncomingAsync_UserState_AppliedToRequest()
        {
            var input = CreateInput();
            input.State.User.Add(InternalModels.Request.IsOldUserKey, true);
            input.State.User.Add(InternalModels.Response.NextTextIndexStorageKey, 4);

            var scopeKey = _fixture.Create<string>();
            input.State.Session.Add(InternalModels.Response.ScopeStorageKey, scopeKey);

            InternalModels.Request captured = null;

            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<InternalModels.Request>()))
                .Callback<InternalModels.Request>(r => captured = r)
                .ReturnsAsync(new InternalModels.Response());


            await _target.ProcessIncomingAsync(input);


            ClassicAssert.True(captured.IsOldUser);
            ClassicAssert.AreEqual(4, captured.NextTextIndex);
            ClassicAssert.AreEqual(scopeKey, captured.ScopeKey);
        }

        [Test]
        public async Task ProcessIncomingAsync_EmptyState_Defaults()
        {
            var input = CreateInput();

            InternalModels.Request captured = null;

            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<InternalModels.Request>()))
                .Callback<InternalModels.Request>(r => captured = r)
                .ReturnsAsync(new InternalModels.Response());


            await _target.ProcessIncomingAsync(input);


            ClassicAssert.False(captured.IsOldUser);
            ClassicAssert.AreEqual(0, captured.NextTextIndex);
            ClassicAssert.Null(captured.ScopeKey);
        }

        #endregion Before: состояние пользователя и сессии

        #region Команда ping

        [Test]
        public async Task ProcessIncomingAsync_PingCommand_PongWithoutConversation()
        {
            var input = CreateInput("ping");


            var result = await _target.ProcessIncomingAsync(input);


            ClassicAssert.AreEqual("pong", result.Response.Text);
            _conversationService.Verify(s => s.GetResponseAsync(It.IsAny<InternalModels.Request>()), Times.Never);
        }

        [Test]
        public async Task ProcessIncomingAsync_PingCommandUpperCase_Pong()
        {
            var input = CreateInput("PING");


            var result = await _target.ProcessIncomingAsync(input);


            ClassicAssert.AreEqual("pong", result.Response.Text);
        }

        [Test]
        public async Task ProcessIncomingAsync_OtherCommand_ConversationCalled()
        {
            var input = CreateInput();

            var response = new InternalModels.Response { Text = _fixture.Create<string>() };

            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<InternalModels.Request>()))
                .ReturnsAsync(response);


            var result = await _target.ProcessIncomingAsync(input);


            ClassicAssert.AreEqual(response.Text, result.Response.Text);
        }

        #endregion Команда ping

        #region AfterAsync

        [Test]
        public async Task ProcessIncomingAsync_Always_WritesStateToOutput()
        {
            var input = CreateInput();

            var response = new InternalModels.Response
            {
                NextTextIndex = 6,
                ScopeKey = _fixture.Create<string>()
            };

            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<InternalModels.Request>()))
                .ReturnsAsync(response);


            var result = await _target.ProcessIncomingAsync(input);


            ClassicAssert.AreEqual(true, result.UserStateUpdate[InternalModels.Request.IsOldUserKey]);
            ClassicAssert.AreEqual(6, result.UserStateUpdate[InternalModels.Response.NextTextIndexStorageKey]);
            ClassicAssert.AreEqual(response.ScopeKey, result.SessionState[InternalModels.Response.ScopeStorageKey]);
        }

        [Test]
        public async Task ProcessIncomingAsync_Always_MarksUserInCache()
        {
            var input = CreateInput();

            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<InternalModels.Request>()))
                .ReturnsAsync(new InternalModels.Response());


            await _target.ProcessIncomingAsync(input);

            await WaitForAsync(() => _cache.Invocations.Any(i => i.Method.Name == nameof(IRedisCacheService.AddAsync)));


            // Отметка о пользователе пишется в кэш без ожидания (fire-and-forget)
            _cache.Verify(
                c => c.AddAsync($"marusia:{input.Session.UserId}", string.Empty, TimeSpan.FromDays(14)),
                Times.Once);
        }

        [Test]
        public async Task ProcessIncomingAsync_Always_CopiesSessionAndVersionToOutput()
        {
            var input = CreateInput();
            input.Version = _fixture.Create<string>();

            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<InternalModels.Request>()))
                .ReturnsAsync(new InternalModels.Response());


            var result = await _target.ProcessIncomingAsync(input);


            ClassicAssert.AreEqual(input.Version, result.Version);
            ClassicAssert.AreEqual(input.Session.UserId, result.Session.UserId);
        }

        #endregion AfterAsync

        private static async Task WaitForAsync(Func<bool> condition, int timeoutMilliseconds = 5000)
        {
            var waited = 0;

            while (!condition() && waited < timeoutMilliseconds)
            {
                await Task.Delay(25);
                waited += 25;
            }
        }
    }
}
