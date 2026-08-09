using System.Collections.Generic;
using System.Linq;
using AutoFixture;
using NUnit.Framework;
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

            Assert.That(result, Is.Null);
        }

        [Test]
        public void ToRequest_Empty_SberSource()
        {
            var result = new Request().ToRequest();

            Assert.That(result.Source, Is.EqualTo(InternalModels.Source.Sber));
        }

        [Test]
        public void ToRequest_UuidWithSub_SubIsUserHash()
        {
            var sub = _fixture.Create<string>();

            var source = new Request { Uuid = new Uuid { Sub = sub, UserId = _fixture.Create<string>() } };


            var result = source.ToRequest();


            Assert.That(result.UserHash, Is.EqualTo(sub));
        }

        [Test]
        public void ToRequest_UuidWithoutSub_UserIdIsUserHash()
        {
            var userId = _fixture.Create<string>();

            var source = new Request { Uuid = new Uuid { UserId = userId } };


            var result = source.ToRequest();


            Assert.That(result.UserHash, Is.EqualTo(userId));
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


            Assert.That(result.HasScreen, Is.True);
        }

        [Test]
        public void ToRequest_NoDevice_HasScreenIsFalse()
        {
            var result = new Request().ToRequest();

            Assert.That(result.HasScreen, Is.False);
        }

        [Test]
        public void ToRequest_Surface_ClientId()
        {
            var surface = _fixture.Create<string>();

            var source = new Request { Payload = new RequestPayload { Device = new Device { Surface = surface } } };


            var result = source.ToRequest();


            Assert.That(result.ClientId, Is.EqualTo(surface));
        }

        [Test]
        public void ToRequest_OfficialCharacterAppeal_OfficialAppeal()
        {
            var source = new Request { Payload = new RequestPayload { Character = new Character { Appeal = "official" } } };


            var result = source.ToRequest();


            Assert.That(result.Appeal, Is.EqualTo(InternalModels.Appeal.Official));
        }

        [Test]
        public void ToRequest_NoCharacter_NoOfficialAppeal()
        {
            var result = new Request().ToRequest();

            Assert.That(result.Appeal, Is.EqualTo(InternalModels.Appeal.NoOfficial));
        }

        [Test]
        public void ToRequest_OtherCharacterAppeal_NoOfficialAppeal()
        {
            var source = new Request { Payload = new RequestPayload { Character = new Character { Appeal = "no_official" } } };


            var result = source.ToRequest();


            Assert.That(result.Appeal, Is.EqualTo(InternalModels.Appeal.NoOfficial));
        }

        #endregion ToRequest

        #region ToRequest: текст запроса

        [Test]
        public void ToRequest_OriginalText_Text()
        {
            var text = _fixture.Create<string>();

            var result = CreateRequest(text).ToRequest();

            Assert.That(result.Text, Is.EqualTo(text));
        }

        [Test]
        public void ToRequest_RatingResultMessage_RatingResultEvent()
        {
            var source = CreateRequest(_fixture.Create<string>());
            source.MessageName = "RATING_RESULT";


            var result = source.ToRequest();


            Assert.That(result.Text, Is.EqualTo("event:rating_result"));
        }

        [Test]
        public void ToRequest_StarsInAsrNormalizedMessage_ObsceneWordReplaced()
        {
            var source = CreateRequest(_fixture.Create<string>(), "какое-то *** слово");


            var result = source.ToRequest();


            Assert.That(result.Text, Is.EqualTo("какое-то кое-что слово"));
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


            Assert.That(result.Text, Is.EqualTo("кое-что"));
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


            Assert.That(result.Text, Is.EqualTo(text));
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


            Assert.That(result.Text, Is.EqualTo(text));
        }

        #endregion ToRequest: текст запроса

        #region ToResponse

        [Test]
        public void ToResponse_Null_Null()
        {
            InternalModels.Response source = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            var result = source.ToResponse();

            Assert.That(result, Is.Null);
        }

        [Test]
        public void ToResponse_AnyResponse_AnswerToUserMessageName()
        {
            var source = new InternalModels.Response { Text = _fixture.Create<string>(), Buttons = new List<InternalModels.Button>() };


            var result = source.ToResponse();


            Assert.That(result.MessageName, Is.EqualTo(MessageNameValues.AnswerToUser));
        }

        [Test]
        public void ToResponse_CallRatingText_CallRatingMessageName()
        {
            var source = new InternalModels.Response { Text = "CALL_RATING", Buttons = new List<InternalModels.Button>() };


            var result = source.ToResponse();


            Assert.That(result.MessageName, Is.EqualTo("CALL_RATING"));
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


            Assert.That(result.Payload.PronounceText, Is.EqualTo("Текст для озвучки"));
            Assert.That(result.Payload.PronounceTextType, Is.EqualTo(PronounceTextTypeValues.Ssml));
            Assert.That(result.Payload.Items.Single().Bubble.Text, Is.EqualTo("Текст на экран"));
        }

        [Test]
        public void ToResponse_NotFinished_AutoListening()
        {
            var source = new InternalModels.Response { Finished = false, Buttons = new List<InternalModels.Button>() };


            var result = source.ToResponse();


            Assert.That(result.Payload.AutoListening, Is.True);
            Assert.That(result.Payload.Finished, Is.False);
        }

        [Test]
        public void ToResponse_Finished_NoAutoListening()
        {
            var source = new InternalModels.Response { Finished = true, Buttons = new List<InternalModels.Button>() };


            var result = source.ToResponse();


            Assert.That(result.Payload.AutoListening, Is.False);
            Assert.That(result.Payload.Finished, Is.True);
        }

        [Test]
        public void ToResponse_SberEmotion_EmotionId()
        {
            var source = new InternalModels.Response { Buttons = new List<InternalModels.Button>() };
            source.Emotions.Add("sberEmotion", "radost");


            var result = source.ToResponse();


            Assert.That(result.Payload.Emotion.EmotionId, Is.EqualTo("radost"));
        }

        [Test]
        public void ToResponse_NoEmotion_NullEmotion()
        {
            var source = new InternalModels.Response { Buttons = new List<InternalModels.Button>() };


            var result = source.ToResponse();


            Assert.That(result.Payload.Emotion, Is.Null);
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

            Assert.That(button.Title, Is.EqualTo("Быстрый ответ"));
            Assert.That(button.Action.Type, Is.EqualTo(ActionTypeValues.Text));
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

            Assert.That(action.Type, Is.EqualTo(ActionTypeValues.DeepLink));
            Assert.That(action.DeepLink, Is.EqualTo(url));
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

            Assert.That(card.Type, Is.EqualTo(CardTypeValues.GridCard));
            Assert.That(card.Columns, Is.EqualTo(2));
            Assert.That(card.Items.Select(i => i.BottomText.Text), Is.EqualTo(new[] { "Первая", "Вторая" }));
            Assert.That(result.Payload.Suggestions.Buttons, Is.Empty);
        }

        [Test]
        public void ToResponse_NoButtons_EmptyCard()
        {
            var source = new InternalModels.Response { Text = "Текст", Buttons = new List<InternalModels.Button>() };


            var result = source.ToResponse();


            // PayloadItem создаёт пустую карточку сам, маппинг её не заполняет
            var card = result.Payload.Items.Single().Card;

            Assert.That(card.Type, Is.Null);
            Assert.That(card.Items == null || card.Items.Length == 0, Is.True);
        }

        #endregion ToResponse: кнопки

        #region FillResponse

        [Test]
        public void FillResponse_Null_Null()
        {
            Request source = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            var result = source.FillResponse(new Response());

            Assert.That(result, Is.Null);
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


            Assert.That(result.SessionId, Is.EqualTo(source.SessionId));
            Assert.That(result.MessageId, Is.EqualTo(source.MessageId));
            Assert.That(result.Uuid, Is.SameAs(source.Uuid));
            Assert.That(result.Payload.Device, Is.SameAs(device));
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


            Assert.That(result, Is.Not.Null);
            Assert.That(result.SessionId, Is.EqualTo(source.SessionId));
        }

        #endregion FillResponse
    }
}
