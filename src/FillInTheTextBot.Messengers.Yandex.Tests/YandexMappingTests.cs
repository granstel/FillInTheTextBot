using AutoFixture;
using AutoFixture.Kernel;
using FillInTheTextBot.Models;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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

            ClassicAssert.IsNull(result);
        }

        [Test]
        public void ToRequest_AllProperties_MappedCorrectly()
        {
            var source = _fixture.Create<InputModel>();

            var result = source.ToRequest();

            ClassicAssert.IsNotNull(result);

            ClassicAssert.AreEqual(source.Session.SkillId, result.ChatHash);
            ClassicAssert.AreEqual(source.Session.UserId, result.UserHash);
            ClassicAssert.AreEqual(source.Request.OriginalUtterance, result.Text);
            ClassicAssert.AreEqual(source.Session.SessionId, result.SessionId);
            ClassicAssert.AreEqual(source.Session.New, result.NewSession);
            ClassicAssert.AreEqual(source.Meta.Locale, result.Language);
            ClassicAssert.AreEqual(result.HasScreen, source.Meta.Interfaces.Screen != null);
            ClassicAssert.AreEqual(result.ClientId, source.Meta.ClientId);
            ClassicAssert.AreEqual(Source.Yandex, result.Source);
            ClassicAssert.AreEqual(Appeal.NoOfficial, result.Appeal);
        }

        [Test]
        public void FillOutput_NullSource_ResultIsNull()
        {
            InputModel source = null;
            OutputModel destination = null;

            var result = source.FillOutput(destination);

            ClassicAssert.IsNull(result);
        }

        [Test]
        public void FillOutput_NullDestination_ResultIsNull()
        {
            var source = new InputModel();
            OutputModel destination = null;

            var result = source.FillOutput(destination);

            ClassicAssert.IsNull(result);
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


            ClassicAssert.AreEqual(input.Session.SessionId, output.Session.SessionId);
            ClassicAssert.AreEqual(input.Session.MessageId, output.Session.MessageId);
            ClassicAssert.AreEqual(input.Version, output.Version);
            ClassicAssert.NotNull(output.Response);
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


            ClassicAssert.NotNull(result?.Buttons);
            ClassicAssert.AreEqual(buttons.Length, result?.Buttons?.Length);
        }
    }
}
