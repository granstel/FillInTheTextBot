using AutoFixture;
using AutoFixture.Kernel;
using FillInTheTextBot.Models;
using NUnit.Framework;
using System.Linq;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Buttons;
using Yandex.Dialogs.Models.Cards;
using Yandex.Dialogs.Models.Input;
using YandexModels = Yandex.Dialogs.Models;

namespace FillInTheTextBot.Messengers.Yandex.Tests
{
    [TestFixture]
    public class YandexMappingTests
    {
        private Fixture _fixture;

        [SetUp]
        public void InitTest()
        {
            _fixture = new Fixture();
            _fixture.Customizations.Add(new TypeRelay(typeof(YandexModels.Buttons.Button), typeof(ResponseButton)));
            _fixture.Customizations.Add(new TypeRelay(typeof(ICard), typeof(ItemsListCard)));
        }

        [Test]
        public void ToRequest_NullSource_ResultIsNull()
        {
            InputModel source = null;

            var result = source.ToRequest();

            Assert.IsNull(result);
        }

        [Test]
        public void ToRequest_AllProperties_MappedCorrectly()
        {
            var source = _fixture.Create<InputModel>();

            var result = source.ToRequest();

            Assert.IsNotNull(result);

            Assert.AreEqual(source.Session.SkillId, result.ChatHash);
            Assert.AreEqual(source.Session.UserId, result.UserHash);
            Assert.AreEqual(source.Request.OriginalUtterance, result.Text);
            Assert.AreEqual(source.Session.SessionId, result.SessionId);
            Assert.AreEqual(source.Session.New, result.NewSession);
            Assert.AreEqual(source.Meta.Locale, result.Language);
            Assert.AreEqual(result.HasScreen, source.Meta.Interfaces.Screen != null);
            Assert.AreEqual(result.ClientId, source.Meta.ClientId);
            Assert.AreEqual(Source.Yandex, result.Source);
            Assert.AreEqual(Appeal.NoOfficial, result.Appeal);
        }

        [Test]
        public void FillOutput_NullSource_ResultIsNull()
        {
            InputModel source = null;
            OutputModel destination = null;

            var result = source.FillOutput(destination);

            Assert.IsNull(result);
        }

        [Test]
        public void FillOutput_NullDestination_ResultIsNull()
        {
            var source = new InputModel();
            OutputModel destination = null;

            var result = source.FillOutput(destination);

            Assert.IsNull(result);
        }

        [Test]
        public void FillOutput_AllParameters_MappedCorrectly()
        {
            var input = _fixture.Create<InputModel>();

            var output = _fixture.Build<OutputModel>()
                .Without(o => o.Session)
                .Without(o => o.Version)
                .Create();


            output = input.FillOutput(output);


            Assert.AreEqual(input.Session.SessionId, output.Session.SessionId);
            Assert.AreEqual(input.Session.MessageId, output.Session.MessageId);
            Assert.AreEqual(input.Version, output.Version);
            Assert.NotNull(output.Response);
        }

        [Test]
        public void Map_ResponseWithButtons_Response()
        {
            var buttons = _fixture.Build<Models.Button>()
                .With(b => b.Text)
                .With(b => b.Url)
                .CreateMany().ToArray();

            var input = _fixture.Build<Models.Response>()
                .With(r => r.Buttons, buttons)
                .Create();


            var result = input.ToResponse();


            Assert.NotNull(result?.Buttons);
            Assert.AreEqual(buttons.Length, result?.Buttons?.Length);
        }
    }
}
