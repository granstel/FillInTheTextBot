using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FillInTheTextBot.Models;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests
{
    public partial class ConversationServiceTests
    {
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
    }
}
