using System.Threading.Tasks;
using System;
using System.Text.Json;
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
            const string rotationState = "test-rotation-state";

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
            var response = new Models.Response { RotationState = "test-rotation-state" };

            _conversationService.Setup(s => s.GetResponseAsync(It.IsAny<Models.Request>())).ReturnsAsync(response);


            var result = await _target.ProcessIncomingAsync(CreateInput());


            Assert.That(result.UserStateUpdate[Models.Response.RotationStateStorageKey], Is.EqualTo(response.RotationState));
            Assert.That(result.ApplicationState[Models.Response.RotationStateStorageKey], Is.EqualTo(response.RotationState));
        }

        [Test]
        public async Task ProcessIncomingAsync_ConversationMetadata_ReadFromSessionState()
        {
            var input = CreateInput();
            input.State.Session.Add(Models.Response.ConversationProviderStorageKey, "StoryDialog");
            input.State.Session.Add(Models.Response.ConversationStateStorageKey, "opaque-session-state");
            input.State.User.Add(Models.Response.ConversationProviderStorageKey, "user-provider");
            input.State.Application.Add(Models.Response.ConversationStateStorageKey, "application-state");
            Models.Request captured = null;
            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<Models.Request>()))
                .Callback<Models.Request>(r => captured = r)
                .ReturnsAsync(new Models.Response());

            await _target.ProcessIncomingAsync(input);

            Assert.That(captured.ConversationProvider, Is.EqualTo("StoryDialog"));
            Assert.That(captured.ConversationState, Is.EqualTo("opaque-session-state"));
        }

        [Test]
        public async Task ProcessIncomingAsync_ConversationMetadataInPersistentState_Ignored()
        {
            var input = CreateInput();
            foreach (var state in new[] { input.State.User, input.State.Application })
            {
                state.Add(Models.Response.ConversationProviderStorageKey, "StoryDialog");
                state.Add(Models.Response.ConversationStateStorageKey, "opaque-persistent-state");
            }
            Models.Request captured = null;
            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<Models.Request>()))
                .Callback<Models.Request>(r => captured = r)
                .ReturnsAsync(new Models.Response());

            var result = await _target.ProcessIncomingAsync(input);

            Assert.That(captured.ConversationProvider, Is.Null);
            Assert.That(captured.ConversationState, Is.Null);
            AssertNoConversationMetadata(result);
        }

        [Test]
        public async Task ProcessIncomingAsync_ConversationMetadata_WrittenOnlyToSessionState()
        {
            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<Models.Request>()))
                .ReturnsAsync(new Models.Response
                {
                    ConversationProvider = "StoryDialog",
                    ConversationState = "opaque-response-state"
                });

            var result = await _target.ProcessIncomingAsync(CreateInput());

            using var json = JsonDocument.Parse(JsonSerializer.Serialize(result));
            var root = json.RootElement;
            Assert.That(root.GetProperty("session_state").GetProperty("CONVERSATIONPROVIDER").GetString(), Is.EqualTo("StoryDialog"));
            Assert.That(root.GetProperty("session_state").GetProperty("CONVERSATIONSTATE").GetString(), Is.EqualTo("opaque-response-state"));
            foreach (var name in new[] { "user_state_update", "application_state" })
            {
                Assert.That(root.GetProperty(name).TryGetProperty("CONVERSATIONPROVIDER", out _), Is.False);
                Assert.That(root.GetProperty(name).TryGetProperty("CONVERSATIONSTATE", out _), Is.False);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ProcessIncomingAsync_LegacyRequest_DoesNotAddConversationMetadata(bool ping)
        {
            var input = CreateInput();
            if (ping)
                input.Request.OriginalUtterance = "ping";
            else
                _conversationService.Setup(s => s.GetResponseAsync(It.IsAny<Models.Request>())).ReturnsAsync(new Models.Response());

            var result = await _target.ProcessIncomingAsync(input);

            AssertNoConversationMetadata(result);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ProcessIncomingAsync_PingOrError_PreservesConversationAndRotationState(bool ping)
        {
            var input = CreateInput();
            input.State.Session.Add(Models.Response.ConversationProviderStorageKey, "StoryDialog");
            input.State.Session.Add(Models.Response.ConversationStateStorageKey, "opaque-request-state");
            input.State.User.Add(Models.Response.NextTextIndexStorageKey, 7);
            input.State.User.Add(Models.Response.RotationStateStorageKey, "rotation-state");
            if (ping)
                input.Request.OriginalUtterance = "ping";
            else
                _conversationService.Setup(s => s.GetResponseAsync(It.IsAny<Models.Request>()))
                    .ThrowsAsync(new InvalidOperationException("synthetic failure"));

            var result = await _target.ProcessIncomingAsync(input);

            using var json = JsonDocument.Parse(JsonSerializer.Serialize(result));
            var root = json.RootElement;
            Assert.That(root.GetProperty("session_state").GetProperty("CONVERSATIONPROVIDER").GetString(), Is.EqualTo("StoryDialog"));
            Assert.That(root.GetProperty("session_state").GetProperty("CONVERSATIONSTATE").GetString(), Is.EqualTo("opaque-request-state"));
            Assert.That(result.UserStateUpdate[Models.Response.NextTextIndexStorageKey], Is.EqualTo(7));
            Assert.That(result.UserStateUpdate[Models.Response.RotationStateStorageKey], Is.EqualTo("rotation-state"));
            if (ping)
            {
                Assert.That(result.Response.Text, Is.EqualTo("pong"));
                _conversationService.Verify(s => s.GetResponseAsync(It.IsAny<Models.Request>()), Times.Never);
            }
        }

        private static void AssertNoConversationMetadata(OutputModel output)
        {
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(output));
            foreach (var name in new[] { "session_state", "user_state_update", "application_state" })
            {
                var state = json.RootElement.GetProperty(name);
                Assert.That(state.TryGetProperty("CONVERSATIONPROVIDER", out _), Is.False);
                Assert.That(state.TryGetProperty("CONVERSATIONSTATE", out _), Is.False);
            }
        }

        [Test]
        public async Task ProcessIncomingAsync_StoryDialogState_FitsAliceSessionStateLimit()
        {
            var conversationState = "{\"c\":\"" + new string('a', 64) +
                "\",\"v\":2,\"s\":1,\"i\":\"moon-picnic\",\"o\":[\"moon-picnic\",\"space-cafe\"]}";
            _conversationService
                .Setup(s => s.GetResponseAsync(It.IsAny<Models.Request>()))
                .ReturnsAsync(new Models.Response
                {
                    ConversationProvider = "StoryDialog",
                    ConversationState = conversationState,
                    ScopeKey = "local-storydialog"
                });

            var result = await _target.ProcessIncomingAsync(CreateInput());

            Assert.That(JsonSerializer.SerializeToUtf8Bytes(result.SessionState).Length, Is.LessThanOrEqualTo(1024));
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
