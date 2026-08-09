using System;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using GranSteL.Helpers.Redis;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Sber.SmartApp.Models;
using InternalModels = FillInTheTextBot.Models;

namespace FillInTheTextBot.Messengers.Sber.Tests
{
    /// <summary>
    /// Характеризующие тесты: фиксируют текущее поведение <see cref="SberService"/>
    /// перед обновлением фреймворка и пакетов.
    /// </summary>
    [TestFixture]
    public class SberServiceTests
    {
        private Mock<Services.IConversationService> _conversationService;
        private Mock<IRedisCacheService> _cache;

        private SberService _target;

        private Fixture _fixture;

        [SetUp]
        public void InitTest()
        {
            _conversationService = new Mock<Services.IConversationService>();
            _cache = new Mock<IRedisCacheService>();

            _target = new SberService(Mock.Of<ILogger<SberService>>(), _conversationService.Object, _cache.Object);

            _fixture = new Fixture { OmitAutoProperties = true };
        }

        private Request CreateInput(string userId = null, bool newSession = false, string characterId = null, string appeal = null)
        {
            return new Request
            {
                Uuid = new Uuid { UserId = userId ?? _fixture.Create<string>() },
                Payload = new RequestPayload
                {
                    NewSession = newSession,
                    Character = new Character { Id = characterId, Appeal = appeal },
                    Message = new Message { OriginalText = _fixture.Create<string>() }
                }
            };
        }

        private static InternalModels.Response CreateResponse(string text = null)
        {
            // ConversationService всегда заполняет Buttons, маппинг Сбера на null не рассчитан
            return new InternalModels.Response
            {
                Text = text,
                Buttons = new System.Collections.Generic.List<InternalModels.Button>()
            };
        }

        #region Before: состояние пользователя из кэша

        [Test]
        public async Task ProcessIncomingAsync_UserStateInCache_AppliedToRequest()
        {
            var input = CreateInput();

            var userState = new InternalModels.UserState
            {
                IsOldUser = true,
                NextTextIndex = 5,
                ScopeKey = _fixture.Create<string>()
            };

            _cache.Setup(c => c.TryGet($"sber:{input.Uuid.UserId}", out userState, It.IsAny<bool>())).Returns(true);

            InternalModels.Request captured = null;

            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<InternalModels.Request>()))
                .Callback<InternalModels.Request>(r => captured = r)
                .ReturnsAsync(CreateResponse());


            await _target.ProcessIncomingAsync(input);


            Assert.That(captured.IsOldUser, Is.True);
            Assert.That(captured.NextTextIndex, Is.EqualTo(5));
            Assert.That(captured.ScopeKey, Is.EqualTo(userState.ScopeKey));
        }

        [Test]
        public async Task ProcessIncomingAsync_NoUserStateInCache_Defaults()
        {
            var input = CreateInput();

            InternalModels.Request captured = null;

            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<InternalModels.Request>()))
                .Callback<InternalModels.Request>(r => captured = r)
                .ReturnsAsync(CreateResponse());


            await _target.ProcessIncomingAsync(input);


            Assert.That(captured.IsOldUser, Is.False);
            Assert.That(captured.NextTextIndex, Is.EqualTo(0));
            Assert.That(captured.ScopeKey, Is.Null);
        }

        #endregion Before: состояние пользователя из кэша

        #region Before: контексты Сбера

        [Test]
        public async Task ProcessIncomingAsync_Character_CharacterContextAdded()
        {
            var characterId = _fixture.Create<string>();

            var input = CreateInput(characterId: characterId);

            InternalModels.Request captured = null;

            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<InternalModels.Request>()))
                .Callback<InternalModels.Request>(r => captured = r)
                .ReturnsAsync(CreateResponse());


            await _target.ProcessIncomingAsync(input);


            var context = captured.RequiredContexts.Single(c => c.Name == $"sber-character-{characterId}");

            Assert.That(context.LifeSpan, Is.EqualTo(2));
        }

        [Test]
        public async Task ProcessIncomingAsync_CharacterAppeal_AppealContextAdded()
        {
            var input = CreateInput(appeal: "official");

            InternalModels.Request captured = null;

            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<InternalModels.Request>()))
                .Callback<InternalModels.Request>(r => captured = r)
                .ReturnsAsync(CreateResponse());


            await _target.ProcessIncomingAsync(input);


            var context = captured.RequiredContexts.Single(c => c.Name == "official");

            Assert.That(context.LifeSpan, Is.EqualTo(2));
        }

        [Test]
        public async Task ProcessIncomingAsync_NoAppeal_NoAppealContext()
        {
            var input = CreateInput();

            InternalModels.Request captured = null;

            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<InternalModels.Request>()))
                .Callback<InternalModels.Request>(r => captured = r)
                .ReturnsAsync(CreateResponse());


            await _target.ProcessIncomingAsync(input);


            Assert.That(captured.RequiredContexts.Count, Is.EqualTo(2), "Ожидаются только sber-character-* и source-Sber");
        }

        #endregion Before: контексты Сбера

        #region Before: идентификатор сессии

        [Test]
        public async Task ProcessIncomingAsync_SessionInCache_SessionReused()
        {
            var input = CreateInput();

            var sessionId = _fixture.Create<string>();

            _cache.Setup(c => c.TryGet($"sber:session:{input.Uuid.UserId}", out sessionId, It.IsAny<bool>())).Returns(true);

            InternalModels.Request captured = null;

            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<InternalModels.Request>()))
                .Callback<InternalModels.Request>(r => captured = r)
                .ReturnsAsync(CreateResponse());


            await _target.ProcessIncomingAsync(input);


            Assert.That(captured.SessionId, Is.EqualTo(sessionId));
            _cache.Verify(c => c.AddAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()), Times.Never);
        }

        [Test]
        public async Task ProcessIncomingAsync_NewSession_SessionRegenerated()
        {
            var input = CreateInput(newSession: true);

            var cachedSessionId = _fixture.Create<string>();

            _cache.Setup(c => c.TryGet($"sber:session:{input.Uuid.UserId}", out cachedSessionId, It.IsAny<bool>())).Returns(true);

            InternalModels.Request captured = null;

            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<InternalModels.Request>()))
                .Callback<InternalModels.Request>(r => captured = r)
                .ReturnsAsync(CreateResponse());


            await _target.ProcessIncomingAsync(input);


            Assert.That(captured.SessionId, Is.Not.EqualTo(cachedSessionId));
            Assert.That(captured.SessionId.Length, Is.EqualTo(32), "Идентификатор сессии — Guid в формате N");
        }

        [Test]
        public async Task ProcessIncomingAsync_NoSessionInCache_SessionCreatedAndCached()
        {
            var input = CreateInput();

            InternalModels.Request captured = null;

            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<InternalModels.Request>()))
                .Callback<InternalModels.Request>(r => captured = r)
                .ReturnsAsync(CreateResponse());


            await _target.ProcessIncomingAsync(input);

            await WaitForAsync(() => _cache.Invocations.Any(i => i.Method.Name == nameof(IRedisCacheService.AddAsync)));


            Assert.That(captured.SessionId, Is.Not.Empty);

            // Кэширование сессии — fire-and-forget
            _cache.Verify(
                c => c.AddAsync($"sber:session:{input.Uuid.UserId}", captured.SessionId, TimeSpan.FromMinutes(5)),
                Times.Once);
        }

        #endregion Before: идентификатор сессии

        #region AfterAsync

        [Test]
        public async Task ProcessIncomingAsync_Always_SavesUserState()
        {
            var input = CreateInput();

            var response = CreateResponse();
            response.NextTextIndex = 3;
            response.ScopeKey = _fixture.Create<string>();

            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<InternalModels.Request>()))
                .ReturnsAsync(response);


            await _target.ProcessIncomingAsync(input);


            _cache.Verify(c => c.TryAddAsync(
                $"sber:{input.Uuid.UserId}",
                It.Is<object>(o => ((InternalModels.UserState)o).IsOldUser
                                   && ((InternalModels.UserState)o).NextTextIndex == 3
                                   && ((InternalModels.UserState)o).ScopeKey == response.ScopeKey),
                TimeSpan.FromDays(14),
                It.IsAny<bool>()), Times.Once);
        }

        [Test]
        public async Task ProcessIncomingAsync_Always_FillsSessionDataToOutput()
        {
            var input = CreateInput();
            input.SessionId = _fixture.Create<string>();
            input.MessageId = _fixture.Create<long>();

            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<InternalModels.Request>()))
                .ReturnsAsync(CreateResponse(_fixture.Create<string>()));


            var result = await _target.ProcessIncomingAsync(input);


            Assert.That(result.SessionId, Is.EqualTo(input.SessionId));
            Assert.That(result.MessageId, Is.EqualTo(input.MessageId));
            Assert.That(result.Uuid, Is.SameAs(input.Uuid));
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
