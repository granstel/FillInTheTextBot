using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using FillInTheTextBot.Models;
using Moq;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests
{
    public partial class ConversationServiceTests
    {
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
    }
}
