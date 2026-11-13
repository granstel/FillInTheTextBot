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
    }
}
