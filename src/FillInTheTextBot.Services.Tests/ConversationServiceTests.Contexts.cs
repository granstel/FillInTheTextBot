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
