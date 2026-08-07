using System.Collections.Generic;
using System.Linq;
using AutoFixture;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Sber.SmartApp.Models;
using Sber.SmartApp.Models.Constants;
using InternalModels = FillInTheTextBot.Models;

namespace FillInTheTextBot.Messengers.Sber.Tests
{
    /// <summary>
    /// Характеризующие тесты: фиксируют текущее поведение <see cref="SberMapping"/>
    /// перед обновлением фреймворка и пакетов.
    /// </summary>
    [TestFixture]
    public class SberMappingTests
    {
        private Fixture _fixture;

        [SetUp]
        public void InitTest()
        {
            _fixture = new Fixture { OmitAutoProperties = true };
        }

        private static Request CreateRequest(string originalText = null, string asrNormalizedMessage = null)
        {
            return new Request
            {
                Payload = new RequestPayload
                {
                    Message = new Message
                    {
                        OriginalText = originalText,
                        AsrNormalizedMessage = asrNormalizedMessage
                    }
                }
            };
        }

        #region ToRequest

        [Test]
        public void ToRequest_Null_Null()
        {
            Request source = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            var result = source.ToRequest();

            ClassicAssert.Null(result);
        }

        [Test]
        public void ToRequest_Empty_SberSource()
        {
            var result = new Request().ToRequest();

            ClassicAssert.AreEqual(InternalModels.Source.Sber, result.Source);
        }

        [Test]
        public void ToRequest_UuidWithSub_SubIsUserHash()
        {
            var sub = _fixture.Create<string>();

            var source = new Request { Uuid = new Uuid { Sub = sub, UserId = _fixture.Create<string>() } };


            var result = source.ToRequest();


            ClassicAssert.AreEqual(sub, result.UserHash);
        }

        [Test]
        public void ToRequest_UuidWithoutSub_UserIdIsUserHash()
        {
            var userId = _fixture.Create<string>();

            var source = new Request { Uuid = new Uuid { UserId = userId } };


            var result = source.ToRequest();


            ClassicAssert.AreEqual(userId, result.UserHash);
        }

        [Test]
        public void ToRequest_ScreenAvailable_HasScreen()
        {
            var source = new Request
            {
                Payload = new RequestPayload
                {
                    Device = new Device
                    {
                        Capabilities = new Capabilities { Screen = new Capability { Available = true } }
                    }
                }
            };


            var result = source.ToRequest();


            ClassicAssert.True(result.HasScreen);
        }

        [Test]
        public void ToRequest_NoDevice_HasScreenIsFalse()
        {
            var result = new Request().ToRequest();

            ClassicAssert.False(result.HasScreen);
        }

        [Test]
        public void ToRequest_Surface_ClientId()
        {
            var surface = _fixture.Create<string>();

            var source = new Request { Payload = new RequestPayload { Device = new Device { Surface = surface } } };


            var result = source.ToRequest();


            ClassicAssert.AreEqual(surface, result.ClientId);
        }

        [Test]
        public void ToRequest_OfficialCharacterAppeal_OfficialAppeal()
        {
            var source = new Request { Payload = new RequestPayload { Character = new Character { Appeal = "official" } } };


            var result = source.ToRequest();


            ClassicAssert.AreEqual(InternalModels.Appeal.Official, result.Appeal);
        }

        [Test]
        public void ToRequest_NoCharacter_NoOfficialAppeal()
        {
            var result = new Request().ToRequest();

            ClassicAssert.AreEqual(InternalModels.Appeal.NoOfficial, result.Appeal);
        }

        [Test]
        public void ToRequest_OtherCharacterAppeal_NoOfficialAppeal()
        {
            var source = new Request { Payload = new RequestPayload { Character = new Character { Appeal = "no_official" } } };


            var result = source.ToRequest();


            ClassicAssert.AreEqual(InternalModels.Appeal.NoOfficial, result.Appeal);
        }

        #endregion ToRequest

        #region ToRequest: текст запроса

        [Test]
        public void ToRequest_OriginalText_Text()
        {
            var text = _fixture.Create<string>();

            var result = CreateRequest(text).ToRequest();

            ClassicAssert.AreEqual(text, result.Text);
        }

        [Test]
        public void ToRequest_RatingResultMessage_RatingResultEvent()
        {
            var source = CreateRequest(_fixture.Create<string>());
            source.MessageName = "RATING_RESULT";


            var result = source.ToRequest();


            ClassicAssert.AreEqual("event:rating_result", result.Text);
        }

        [Test]
        public void ToRequest_StarsInAsrNormalizedMessage_ObsceneWordReplaced()
        {
            var source = CreateRequest(_fixture.Create<string>(), "какое-то *** слово");


            var result = source.ToRequest();


            ClassicAssert.AreEqual("какое-то кое-что слово", result.Text);
        }

        [Test]
        public void ToRequest_ObsceneProbability_ObsceneWord()
        {
            var source = CreateRequest(_fixture.Create<string>());
            source.Payload.Annotations = new Annotations
            {
                CensorData = new Annotation
                {
                    Classes = new[] { "politics", "obscene" },
                    Probas = new[] { 0f, 1f }
                }
            };


            var result = source.ToRequest();


            ClassicAssert.AreEqual("кое-что", result.Text);
        }

        [Test]
        public void ToRequest_NoObsceneProbability_OriginalText()
        {
            var text = _fixture.Create<string>();

            var source = CreateRequest(text);
            source.Payload.Annotations = new Annotations
            {
                CensorData = new Annotation
                {
                    Classes = new[] { "politics", "obscene" },
                    Probas = new[] { 0f, 0.5f }
                }
            };


            var result = source.ToRequest();


            ClassicAssert.AreEqual(text, result.Text);
        }

        [Test]
        public void ToRequest_MalformedCensorData_OriginalTextWithoutException()
        {
            var text = _fixture.Create<string>();

            var source = CreateRequest(text);
            source.Payload.Annotations = new Annotations
            {
                CensorData = new Annotation
                {
                    Classes = new[] { "obscene" },
                    Probas = new float[0]
                }
            };


            var result = source.ToRequest();


            ClassicAssert.AreEqual(text, result.Text);
        }

        #endregion ToRequest: текст запроса

        #region ToResponse

        [Test]
        public void ToResponse_Null_Null()
        {
            InternalModels.Response source = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            var result = source.ToResponse();

            ClassicAssert.Null(result);
        }

        [Test]
        public void ToResponse_AnyResponse_AnswerToUserMessageName()
        {
            var source = new InternalModels.Response { Text = _fixture.Create<string>(), Buttons = new List<InternalModels.Button>() };


            var result = source.ToResponse();


            ClassicAssert.AreEqual(MessageNameValues.AnswerToUser, result.MessageName);
        }

        [Test]
        public void ToResponse_CallRatingText_CallRatingMessageName()
        {
            var source = new InternalModels.Response { Text = "CALL_RATING", Buttons = new List<InternalModels.Button>() };


            var result = source.ToResponse();


            ClassicAssert.AreEqual("CALL_RATING", result.MessageName);
        }

        [Test]
        public void ToResponse_TextAndAlternativeText_BubbleAndPronounceText()
        {
            var source = new InternalModels.Response
            {
                Text = "Текст на экран",
                AlternativeText = "Текст для озвучки",
                Buttons = new List<InternalModels.Button>()
            };


            var result = source.ToResponse();


            ClassicAssert.AreEqual("Текст для озвучки", result.Payload.PronounceText);
            ClassicAssert.AreEqual(PronounceTextTypeValues.Ssml, result.Payload.PronounceTextType);
            ClassicAssert.AreEqual("Текст на экран", result.Payload.Items.Single().Bubble.Text);
        }

        [Test]
        public void ToResponse_NotFinished_AutoListening()
        {
            var source = new InternalModels.Response { Finished = false, Buttons = new List<InternalModels.Button>() };


            var result = source.ToResponse();


            ClassicAssert.True(result.Payload.AutoListening);
            ClassicAssert.False(result.Payload.Finished);
        }

        [Test]
        public void ToResponse_Finished_NoAutoListening()
        {
            var source = new InternalModels.Response { Finished = true, Buttons = new List<InternalModels.Button>() };


            var result = source.ToResponse();


            ClassicAssert.False(result.Payload.AutoListening);
            ClassicAssert.True(result.Payload.Finished);
        }

        [Test]
        public void ToResponse_SberEmotion_EmotionId()
        {
            var source = new InternalModels.Response { Buttons = new List<InternalModels.Button>() };
            source.Emotions.Add("sberEmotion", "radost");


            var result = source.ToResponse();


            ClassicAssert.AreEqual("radost", result.Payload.Emotion.EmotionId);
        }

        [Test]
        public void ToResponse_NoEmotion_NullEmotion()
        {
            var source = new InternalModels.Response { Buttons = new List<InternalModels.Button>() };


            var result = source.ToResponse();


            ClassicAssert.Null(result.Payload.Emotion);
        }

        #endregion ToResponse

        #region ToResponse: кнопки

        [Test]
        public void ToResponse_QuickReplyButtons_Suggestions()
        {
            var source = new InternalModels.Response
            {
                Buttons = new List<InternalModels.Button>
                {
                    new InternalModels.Button { Text = "Быстрый ответ", IsQuickReply = true }
                }
            };


            var result = source.ToResponse();


            var button = result.Payload.Suggestions.Buttons.Single();

            ClassicAssert.AreEqual("Быстрый ответ", button.Title);
            ClassicAssert.AreEqual(ActionTypeValues.Text, button.Action.Type);
        }

        [Test]
        public void ToResponse_QuickReplyButtonWithUrl_DeepLinkAction()
        {
            var url = _fixture.Create<string>();

            var source = new InternalModels.Response
            {
                Buttons = new List<InternalModels.Button>
                {
                    new InternalModels.Button { Text = "Ссылка", Url = url, IsQuickReply = true }
                }
            };


            var result = source.ToResponse();


            var action = result.Payload.Suggestions.Buttons.Single().Action;

            ClassicAssert.AreEqual(ActionTypeValues.DeepLink, action.Type);
            ClassicAssert.AreEqual(url, action.DeepLink);
        }

        [Test]
        public void ToResponse_NonQuickReplyButtons_GridCard()
        {
            var source = new InternalModels.Response
            {
                Text = "Текст",
                Buttons = new List<InternalModels.Button>
                {
                    new InternalModels.Button { Text = "Первая" },
                    new InternalModels.Button { Text = "Вторая" }
                }
            };


            var result = source.ToResponse();


            var card = result.Payload.Items.Single().Card;

            ClassicAssert.AreEqual(CardTypeValues.GridCard, card.Type);
            ClassicAssert.AreEqual(2, card.Columns);
            CollectionAssert.AreEqual(new[] { "Первая", "Вторая" }, card.Items.Select(i => i.BottomText.Text));
            ClassicAssert.IsEmpty(result.Payload.Suggestions.Buttons);
        }

        [Test]
        public void ToResponse_NoButtons_EmptyCard()
        {
            var source = new InternalModels.Response { Text = "Текст", Buttons = new List<InternalModels.Button>() };


            var result = source.ToResponse();


            // PayloadItem создаёт пустую карточку сам, маппинг её не заполняет
            var card = result.Payload.Items.Single().Card;

            ClassicAssert.Null(card.Type);
            ClassicAssert.True(card.Items == null || card.Items.Length == 0);
        }

        #endregion ToResponse: кнопки

        #region FillResponse

        [Test]
        public void FillResponse_Null_Null()
        {
            Request source = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            var result = source.FillResponse(new Response());

            ClassicAssert.Null(result);
        }

        [Test]
        public void FillResponse_SessionData_CopiedToResponse()
        {
            var device = new Device { Surface = _fixture.Create<string>() };

            var source = new Request
            {
                SessionId = _fixture.Create<string>(),
                MessageId = _fixture.Create<long>(),
                Uuid = new Uuid { UserId = _fixture.Create<string>() },
                Payload = new RequestPayload { Device = device }
            };

            var destination = new Response();


            var result = source.FillResponse(destination);


            ClassicAssert.AreEqual(source.SessionId, result.SessionId);
            ClassicAssert.AreEqual(source.MessageId, result.MessageId);
            ClassicAssert.AreSame(source.Uuid, result.Uuid);
            ClassicAssert.AreSame(device, result.Payload.Device);
        }

        [Test]
        public void FillResponse_NullDestination_NewResponseCreated()
        {
            var source = new Request
            {
                SessionId = _fixture.Create<string>(),
                Payload = new RequestPayload { Device = new Device() }
            };


            var result = source.FillResponse(null);


            ClassicAssert.NotNull(result);
            ClassicAssert.AreEqual(source.SessionId, result.SessionId);
        }

        #endregion FillResponse
    }
}
