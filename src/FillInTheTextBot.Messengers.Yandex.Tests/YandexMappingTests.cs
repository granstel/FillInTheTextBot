using AutoFixture;
using AutoFixture.Kernel;
using NUnit.Framework;
using System.Linq;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Buttons;
using Yandex.Dialogs.Models.Cards;
using Yandex.Dialogs.Models.Input;

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
            _fixture.Customizations.Add(new TypeRelay(typeof(Button), typeof(ResponseButton)));
            _fixture.Customizations.Add(new TypeRelay(typeof(ICard), typeof(ItemsListCard)));
        }

        [Test]
        public void Map_InputToOutput()
        {
            var input = _fixture.Create<InputModel>();

            var output = _fixture.Build<OutputModel>()
                .Without(o => o.Session)
                .Without(o => o.Version)
                .Create();


            output = input.ToOutput(output);


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
