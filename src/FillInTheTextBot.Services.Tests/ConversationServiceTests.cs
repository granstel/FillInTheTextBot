using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services.Configuration;
using GranSteL.Helpers.Redis;
using Moq;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests
{
    /// <summary>
    /// Характеризующие тесты: фиксируют текущее поведение <see cref="ConversationService"/>
    /// перед обновлением фреймворка и пакетов.
    /// </summary>
    [TestFixture]
    public class ConversationServiceTests
    {
        private Mock<IDialogflowService> _dialogflowService;
        private Mock<IRedisCacheService> _cache;

        private ConversationConfiguration _configuration;

        private ConversationService _target;

        private Fixture _fixture;

        [SetUp]
        public void InitTest()
        {
            _dialogflowService = new Mock<IDialogflowService>();
            _cache = new Mock<IRedisCacheService>();

            _configuration = new ConversationConfiguration();

            _target = new ConversationService(_configuration, _dialogflowService.Object, _cache.Object);

            _fixture = new Fixture { OmitAutoProperties = true };
        }

        private void SetupDialog(Dialog dialog)
        {
            _dialogflowService.Setup(s => s.GetResponseAsync(It.IsAny<Request>())).ReturnsAsync(dialog);
        }

        #region Маппинг Dialog -> Response

        [Test]
        public async Task GetResponseAsync_Dialog_MapsToResponse()
        {
            var buttons = new[] { new Button { Text = _fixture.Create<string>() } };

            var dialog = new Dialog
            {
                Response = _fixture.Create<string>(),
                EndConversation = true,
                Buttons = buttons,
                ScopeKey = _fixture.Create<string>()
            };

            SetupDialog(dialog);


            var result = await _target.GetResponseAsync(new Request());


            Assert.That(result.Text, Is.EqualTo(dialog.Response));
            Assert.That(result.Finished, Is.True);
            Assert.That(result.ScopeKey, Is.EqualTo(dialog.ScopeKey));
            Assert.That(result.Buttons.Select(b => b.Text), Is.EquivalentTo(buttons.Select(b => b.Text)));
        }

        [Test]
        public async Task GetResponseAsync_NullDialog_EmptyResponse()
        {
            SetupDialog(null);


            var result = await _target.GetResponseAsync(new Request());


            Assert.That(result.Text, Is.Null);
            Assert.That(result.Finished, Is.False);
            Assert.That(result.Buttons, Is.Empty);
        }

        [Test]
        public async Task GetResponseAsync_Always_CopiesNextTextIndexFromRequest()
        {
            SetupDialog(new Dialog());

            var request = new Request { NextTextIndex = 7 };


            var result = await _target.GetResponseAsync(request);


            Assert.That(result.NextTextIndex, Is.EqualTo(7));
        }

        #endregion Маппинг Dialog -> Response

        #region resetTextIndex

        [Test]
        public async Task GetResponseAsync_ResetTextIndexParameter_ResetsIndexToZero()
        {
            var dialog = new Dialog();
            dialog.Parameters.Add("resetTextIndex", bool.TrueString);

            SetupDialog(dialog);

            var request = new Request { NextTextIndex = 42 };


            var result = await _target.GetResponseAsync(request);


            Assert.That(result.NextTextIndex, Is.EqualTo(0));
            Assert.That(request.NextTextIndex, Is.EqualTo(0));
        }

        [Test]
        public async Task GetResponseAsync_ResetTextIndexParameterLowerCase_ResetsIndexToZero()
        {
            var dialog = new Dialog();
            dialog.Parameters.Add("resetTextIndex", "true");

            SetupDialog(dialog);

            var request = new Request { NextTextIndex = 42 };


            var result = await _target.GetResponseAsync(request);


            Assert.That(result.NextTextIndex, Is.EqualTo(0));
        }

        [Test]
        public async Task GetResponseAsync_ResetTextIndexFalse_KeepsIndex()
        {
            var dialog = new Dialog();
            dialog.Parameters.Add("resetTextIndex", bool.FalseString);

            SetupDialog(dialog);

            var request = new Request { NextTextIndex = 42 };


            var result = await _target.GetResponseAsync(request);


            Assert.That(result.NextTextIndex, Is.EqualTo(42));
        }

        #endregion resetTextIndex

        #region GetText

        [Test]
        public async Task GetResponseAsync_GetTextActionWithTextKey_ConcatenatesStartTextNameAndResponse()
        {
            var textKey = _fixture.Create<string>();

            var dialog = new Dialog
            {
                Action = "GetText",
                Response = "Поехали!"
            };
            dialog.Parameters.Add("textKey", textKey);

            SetupDialog(dialog);

            var textDialog = new Dialog { Response = "Текст истории" };
            textDialog.Parameters.Add("text-name", "Название");

            _dialogflowService
                .Setup(s => s.GetResponseAsync($"event:{textKey}", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<Context>>()))
                .ReturnsAsync(textDialog);


            var result = await _target.GetResponseAsync(new Request());


            Assert.That(result.Text, Is.EqualTo("Поехали! Название Текст истории"));
        }

        [Test]
        public async Task GetResponseAsync_GetTextActionWithoutTextKey_TakesKeyFromCacheByIndex()
        {
            var dialog = new Dialog { Action = "GetText", Response = "Поехали!" };

            SetupDialog(dialog);

            var texts = new[] { "text-1", "text-2", "text-3" };
            _cache.Setup(c => c.TryGet("Texts", out texts, It.IsAny<bool>())).Returns(true);

            var textDialog = new Dialog { Response = "Текст истории" };

            _dialogflowService
                .Setup(s => s.GetResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<Context>>()))
                .ReturnsAsync(textDialog);

            var request = new Request { NextTextIndex = 1 };


            var result = await _target.GetResponseAsync(request);


            _dialogflowService.Verify(s => s.GetResponseAsync("event:text-2", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<Context>>()), Times.Once);
            Assert.That(result.NextTextIndex, Is.EqualTo(2), "Индекс следующего текста должен увеличиться на единицу");
        }

        [Test]
        public async Task GetResponseAsync_GetTextActionIndexOutOfRange_TextsOverKey()
        {
            var dialog = new Dialog { Action = "GetText", Response = "Поехали!" };

            SetupDialog(dialog);

            var texts = new[] { "text-1" };
            _cache.Setup(c => c.TryGet("Texts", out texts, It.IsAny<bool>())).Returns(true);

            _dialogflowService
                .Setup(s => s.GetResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<Context>>()))
                .ReturnsAsync(new Dialog());

            var request = new Request { NextTextIndex = 5 };


            await _target.GetResponseAsync(request);


            _dialogflowService.Verify(s => s.GetResponseAsync("event:texts-over", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<Context>>()), Times.Once);
        }

        [Test]
        public async Task GetResponseAsync_GetTextActionAndNoTextsInCache_StubAnswer()
        {
            var dialog = new Dialog { Action = "GetText", Response = "Поехали!" };

            SetupDialog(dialog);

            string[] texts = null;
            _cache.Setup(c => c.TryGet("Texts", out texts, It.IsAny<bool>())).Returns(false);


            var result = await _target.GetResponseAsync(new Request());


            Assert.That(result.Text, Is.EqualTo("Что-то у меня не нашлось никаких текстов..."));
            _dialogflowService.Verify(
                s => s.GetResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<Context>>()),
                Times.Never);
        }

        [Test]
        public async Task GetResponseAsync_GetTextActionForStoryWithComputation_PassesComputedContext()
        {
            // text-37-1 — единственная история со встроенным вычислителем контекста
            var dialog = new Dialog { Action = "GetText", Response = "Поехали!" };
            dialog.Parameters.Add("textKey", "text-37-1");

            SetupDialog(dialog);

            IEnumerable<Context> passedContexts = null;

            _dialogflowService
                .Setup(s => s.GetResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<Context>>()))
                .Callback<string, string, string, IEnumerable<Context>>((_, _, _, contexts) => passedContexts = contexts)
                .ReturnsAsync(new Dialog());


            await _target.GetResponseAsync(new Request());


            var expected = StoryComputations.TryBuildContext("text-37-1", out var computedContext);

            if (expected)
            {
                Assert.That(passedContexts, Is.Not.Null);
                Assert.That(passedContexts.Single().Name, Is.EqualTo(computedContext.Name));
            }
            else
            {
                Assert.That(passedContexts, Is.Null);
            }
        }

        [Test]
        public async Task GetResponseAsync_GetTextActionForStoryWithoutComputation_NoRequiredContexts()
        {
            var dialog = new Dialog { Action = "GetText", Response = "Поехали!" };
            dialog.Parameters.Add("textKey", "text-1");

            SetupDialog(dialog);

            IEnumerable<Context> passedContexts = new List<Context>();

            _dialogflowService
                .Setup(s => s.GetResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<Context>>()))
                .Callback<string, string, string, IEnumerable<Context>>((_, _, _, contexts) => passedContexts = contexts)
                .ReturnsAsync(new Dialog());


            await _target.GetResponseAsync(new Request());


            Assert.That(passedContexts, Is.Null);
        }

        #endregion GetText

        #region CALL_RATING

        [Test]
        public async Task GetResponseAsync_CallRatingAction_TextIsCallRating()
        {
            var dialog = new Dialog
            {
                Action = "CALL_RATING",
                Response = _fixture.Create<string>()
            };

            SetupDialog(dialog);


            var result = await _target.GetResponseAsync(new Request());


            Assert.That(result.Text, Is.EqualTo("CALL_RATING"));
        }

        #endregion CALL_RATING

        #region CancelsSlotFilling

        [Test]
        public async Task GetResponseAsync_CancelsSlotFilling_AppendsAnswerAndReplacesButtons()
        {
            var dialog = new Dialog
            {
                Response = "Первый",
                CancelsSlotFilling = true,
                Buttons = new[] { new Button { Text = "Старая" } }
            };

            SetupDialog(dialog);

            var cancelsDialog = new Dialog
            {
                Response = "Второй",
                Buttons = new[] { new Button { Text = "Новая" } }
            };

            _dialogflowService
                .Setup(s => s.GetResponseAsync("event:CancelsSlotFilling", It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(cancelsDialog);


            var result = await _target.GetResponseAsync(new Request());


            Assert.That(result.Text, Is.EqualTo("Первый Второй"));
            Assert.That(result.Buttons.Single().Text, Is.EqualTo("Новая"));
        }

        [Test]
        public async Task GetResponseAsync_NoCancelsSlotFilling_NoAdditionalRequest()
        {
            SetupDialog(new Dialog { Response = "Первый" });


            var result = await _target.GetResponseAsync(new Request());


            Assert.That(result.Text, Is.EqualTo("Первый"));
            _dialogflowService.Verify(s => s.GetResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        #endregion CancelsSlotFilling

        #region Appeal

        [Test]
        public async Task GetResponseAsync_OfficialAppeal_ReplacesWords()
        {
            SetupDialog(new Dialog { Response = "Привет, ты готов?" });

            IDictionary<string, string> appealWords = new Dictionary<string, string>
            {
                { "Привет", "Здравствуйте" },
                { "ты", "вы" }
            };

            _cache.Setup(c => c.TryGet($"AppealWords-{Appeal.Official}", out appealWords, It.IsAny<bool>())).Returns(true);

            var request = new Request { Appeal = Appeal.Official };


            var result = await _target.GetResponseAsync(request);


            Assert.That(result.Text, Is.EqualTo("Здравствуйте, вы готов?"));
        }

        [Test]
        public async Task GetResponseAsync_NonOfficialAppeal_CacheNotRequested()
        {
            SetupDialog(new Dialog { Response = "Привет, ты готов?" });

            var request = new Request { Appeal = Appeal.NoOfficial };


            var result = await _target.GetResponseAsync(request);


            Assert.That(result.Text, Is.EqualTo("Привет, ты готов?"));

            IDictionary<string, string> ignored = null;
            _cache.Verify(c => c.TryGet(It.IsAny<string>(), out ignored, It.IsAny<bool>()), Times.Never);
        }

        [Test]
        public async Task GetResponseAsync_OfficialAppealAndEmptyWords_TextUnchanged()
        {
            SetupDialog(new Dialog { Response = "Привет" });

            IDictionary<string, string> appealWords = new Dictionary<string, string>();
            _cache.Setup(c => c.TryGet($"AppealWords-{Appeal.Official}", out appealWords, It.IsAny<bool>())).Returns(true);

            var request = new Request { Appeal = Appeal.Official };


            var result = await _target.GetResponseAsync(request);


            Assert.That(result.Text, Is.EqualTo("Привет"));
        }

        #endregion Appeal

        #region Кнопки из payload

        [Test]
        public async Task GetResponseAsync_ButtonsInPayloadForSource_AppendedAfterDialogButtons()
        {
            var payload = new Payload
            {
                {
                    Source.Yandex,
                    new SourcePayload { Buttons = new List<Button> { new Button { Text = "Из payload" } } }
                }
            };

            var dialog = new Dialog
            {
                Buttons = new[] { new Button { Text = "Из диалога" } },
                Payload = payload
            };

            SetupDialog(dialog);

            var request = new Request { Source = Source.Yandex };


            var result = await _target.GetResponseAsync(request);


            Assert.That(result.Buttons.Select(b => b.Text), Is.EqualTo(new[] { "Из диалога", "Из payload" }));
        }

        [Test]
        public async Task GetResponseAsync_ButtonsForSourceAndDefault_BothAppended()
        {
            var payload = new Payload
            {
                {
                    Source.Yandex,
                    new SourcePayload { Buttons = new List<Button> { new Button { Text = "Яндекс" } } }
                },
                {
                    Source.Default,
                    new SourcePayload { Buttons = new List<Button> { new Button { Text = "Общая" } } }
                }
            };

            SetupDialog(new Dialog { Payload = payload });

            var request = new Request { Source = Source.Yandex };


            var result = await _target.GetResponseAsync(request);


            Assert.That(result.Buttons.Select(b => b.Text), Is.EqualTo(new[] { "Яндекс", "Общая" }));
        }

        [Test]
        public async Task GetResponseAsync_ButtonWithEmptyText_Filtered()
        {
            var dialog = new Dialog
            {
                Buttons = new[]
                {
                    new Button { Text = "Видимая" },
                    new Button { Text = string.Empty },
                    new Button { Text = null }
                }
            };

            SetupDialog(dialog);


            var result = await _target.GetResponseAsync(new Request());


            Assert.That(result.Buttons.Single().Text, Is.EqualTo("Видимая"));
        }

        #endregion Кнопки из payload

        #region Replacements из payload

        [Test]
        public async Task GetResponseAsync_ReplacementsForSource_TextWithoutBracketsAlternativeWithValue()
        {
            var payload = new Payload
            {
                {
                    Source.Sber,
                    new SourcePayload
                    {
                        Replacements = new Dictionary<string, string> { { "<имя>", "Вася" } }
                    }
                }
            };

            SetupDialog(new Dialog { Response = "Привет, <имя>!", Payload = payload });

            var request = new Request { Source = Source.Sber };


            var result = await _target.GetResponseAsync(request);


            Assert.That(result.Text, Is.EqualTo("Привет, имя!"));
            Assert.That(result.AlternativeText, Is.EqualTo("Привет, Вася!"));
        }

        [Test]
        public async Task GetResponseAsync_ReplacementsOnlyForDefault_UsedAsFallback()
        {
            var payload = new Payload
            {
                {
                    Source.Default,
                    new SourcePayload
                    {
                        Replacements = new Dictionary<string, string> { { "<имя>", "Вася" } }
                    }
                }
            };

            SetupDialog(new Dialog { Response = "Привет, <имя>!", Payload = payload });

            var request = new Request { Source = Source.Sber };


            var result = await _target.GetResponseAsync(request);


            Assert.That(result.Text, Is.EqualTo("Привет, имя!"));
            Assert.That(result.AlternativeText, Is.EqualTo("Привет, Вася!"));
        }

        [Test]
        public async Task GetResponseAsync_NoPayload_AlternativeTextEqualsText()
        {
            SetupDialog(new Dialog { Response = "Привет" });


            var result = await _target.GetResponseAsync(new Request());


            Assert.That(result.Text, Is.EqualTo("Привет"));
            Assert.That(result.AlternativeText, Is.EqualTo("Привет"));
        }

        #endregion Replacements из payload

        #region ResetContexts

        [Test]
        public async Task GetResponseAsync_TextFromResetContextWords_ResetContextsIsTrue()
        {
            _configuration.ResetContextWords = new[] { "помощь", "выход" };

            SetupDialog(new Dialog());

            var request = new Request { Text = "ПОМОЩЬ" };


            await _target.GetResponseAsync(request);


            Assert.That(request.ResetContexts, Is.True, "Сравнение слов сброса должно быть регистронезависимым");
        }

        [Test]
        public async Task GetResponseAsync_TextIsNotResetWord_ResetContextsIsFalse()
        {
            _configuration.ResetContextWords = new[] { "помощь" };

            SetupDialog(new Dialog());

            var request = new Request { Text = _fixture.Create<string>() };


            await _target.GetResponseAsync(request);


            Assert.That(request.ResetContexts, Is.False);
        }

        #endregion ResetContexts

        #region Эмоции

        [Test]
        public async Task GetResponseAsync_EmotionInDialogParameters_TakenFromDialog()
        {
            var dialog = new Dialog();
            dialog.Parameters.Add("sberEmotion", "radost");

            SetupDialog(dialog);


            var result = await _target.GetResponseAsync(new Request());


            Assert.That(result.Emotions["sberEmotion"], Is.EqualTo("radost"));
        }

        [Test]
        public async Task GetResponseAsync_ReadyStoryWithoutEmotion_RandomEmotionAdded()
        {
            var dialog = new Dialog { ParametersIncomplete = false };
            dialog.Parameters.Add("text-name", "Название");

            SetupDialog(dialog);


            var result = await _target.GetResponseAsync(new Request());


            Assert.That(result.Emotions.ContainsKey("sberEmotion"), Is.True);
            Assert.That(result.Emotions["sberEmotion"], Is.Not.Empty);
        }

        [Test]
        public async Task GetResponseAsync_NoTextName_NoEmotions()
        {
            SetupDialog(new Dialog { ParametersIncomplete = false });


            var result = await _target.GetResponseAsync(new Request());


            Assert.That(result.Emotions, Is.Empty);
        }

        [Test]
        public async Task GetResponseAsync_ParametersIncomplete_NoEmotions()
        {
            var dialog = new Dialog { ParametersIncomplete = true };
            dialog.Parameters.Add("text-name", "Название");

            SetupDialog(dialog);


            var result = await _target.GetResponseAsync(new Request());


            Assert.That(result.Emotions, Is.Empty);
        }

        #endregion Эмоции

        #region saveToRepeat

        [Test]
        public async Task GetResponseAsync_SaveToRepeatAction_SetsSavedTextContext()
        {
            var dialog = new Dialog
            {
                Action = "saveToRepeat",
                ParametersIncomplete = false,
                Response = "История"
            };

            SetupDialog(dialog);

            var request = new Request
            {
                SessionId = _fixture.Create<string>(),
                ScopeKey = _fixture.Create<string>()
            };


            await _target.GetResponseAsync(request);

            // SetContextAsync вызывается без ожидания (fire-and-forget)
            await WaitForAsync(() => _dialogflowService.Invocations.Any(i => i.Method.Name == nameof(IDialogflowService.SetContextAsync)));


            _dialogflowService.Verify(s => s.SetContextAsync(
                request.SessionId,
                request.ScopeKey,
                "savedText",
                5,
                It.Is<IDictionary<string, string>>(p => p["text"] == "История" && p["alternativeText"] == "История")), Times.Once);
        }

        [Test]
        public async Task GetResponseAsync_SaveToRepeatWithIncompleteParameters_ContextNotSet()
        {
            var dialog = new Dialog
            {
                Action = "saveToRepeat",
                ParametersIncomplete = true
            };

            SetupDialog(dialog);


            await _target.GetResponseAsync(new Request());


            _dialogflowService.Verify(s => s.SetContextAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IDictionary<string, string>>()), Times.Never);
        }

        [Test]
        public async Task GetResponseAsync_OtherAction_ContextNotSet()
        {
            SetupDialog(new Dialog { Action = "GetText", ParametersIncomplete = false });

            var texts = new[] { "text-1" };
            _cache.Setup(c => c.TryGet("Texts", out texts, It.IsAny<bool>())).Returns(true);

            _dialogflowService
                .Setup(s => s.GetResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<Context>>()))
                .ReturnsAsync(new Dialog());


            await _target.GetResponseAsync(new Request());


            _dialogflowService.Verify(s => s.SetContextAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IDictionary<string, string>>()), Times.Never);
        }

        #endregion saveToRepeat

        private static async Task WaitForAsync(System.Func<bool> condition, int timeoutMilliseconds = 5000)
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
