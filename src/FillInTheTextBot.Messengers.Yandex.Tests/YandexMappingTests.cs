using System.Linq;
using AutoFixture;
using AutoFixture.Kernel;
using FillInTheTextBot.Models;
using NUnit.Framework;
using Yandex.Dialogs.Models.Buttons;
using Yandex.Dialogs.Models.Cards;
using Yandex.Dialogs.Models.Input;
using Button = Yandex.Dialogs.Models.Buttons.Button;
using YandexModels = Yandex.Dialogs.Models;

namespace FillInTheTextBot.Messengers.Yandex.Tests;

[TestFixture]
public class YandexMappingTests
{
    [SetUp]
    public void InitTest()
    {
        _fixture = new Fixture();
        _fixture.Customizations.Add(new TypeRelay(typeof(Button), typeof(ResponseButton)));
        _fixture.Customizations.Add(new TypeRelay(typeof(ICard), typeof(ItemsListCard)));
    }

    private Fixture _fixture;

    [Test]
    public void ToRequest_NullSource_ResultIsNull()
    {
        InputModel source = null;

        var result = source.ToRequest();

        Assert.That(result, Is.Null);
    }

    [Test]
    public void ToRequest_AllProperties_MappedCorrectly()
    {
        var source = _fixture.Create<InputModel>();

        var result = source.ToRequest();

        Assert.That(result, Is.Not.Null);

        Assert.That(result.ChatHash, Is.EqualTo(source.Session.SkillId));
        Assert.That(result.UserHash, Is.EqualTo(source.Session.UserId));
        Assert.That(result.Text, Is.EqualTo(source.Request.OriginalUtterance));
        Assert.That(result.SessionId, Is.EqualTo(source.Session.SessionId));
        Assert.That(result.NewSession, Is.EqualTo(source.Session.New));
        Assert.That(result.Language, Is.EqualTo(source.Meta.Locale));
        Assert.That(result.HasScreen, Is.EqualTo(source.Meta.Interfaces.Screen != null));
        Assert.That(result.ClientId, Is.EqualTo(source.Meta.ClientId));
        Assert.That(result.Source, Is.EqualTo(Source.Yandex));
        Assert.That(result.Appeal, Is.EqualTo(Appeal.NoOfficial));
    }

    [Test]
    public void FillOutput_NullSource_ResultIsNull()
    {
        InputModel source = null;
        YandexModels.OutputModel destination = null;

        var result = source.FillOutput(destination);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void FillOutput_NullDestination_ResultIsNull()
    {
        var source = new InputModel();
        YandexModels.OutputModel destination = null;

        var result = source.FillOutput(destination);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void FillOutput_AllParameters_MappedCorrectly()
    {
        var input = _fixture.Create<InputModel>();

        var output = _fixture.Build<YandexModels.OutputModel>()
            .Without(o => o.Session)
            .Without(o => o.Version)
            .Create();


        output = input.FillOutput(output);


        Assert.That(output.Session.SessionId, Is.EqualTo(input.Session.SessionId));
        Assert.That(output.Session.MessageId, Is.EqualTo(input.Session.MessageId));
        Assert.That(output.Version, Is.EqualTo(input.Version));
        Assert.That(output.Response, Is.Not.Null);
    }

    [Test]
    public void Map_ResponseWithButtons_Response()
    {
        var buttons = _fixture.Build<Models.Button>()
            .With(b => b.Text)
            .With(b => b.Url)
            .CreateMany().ToArray();

        var input = _fixture.Build<Response>()
            .With(r => r.Buttons, buttons)
            .Create();


        var result = input.ToResponse();


        Assert.That(result?.Buttons, Is.Not.Null);
        Assert.That(result?.Buttons?.Length, Is.EqualTo(buttons.Length));
    }
}