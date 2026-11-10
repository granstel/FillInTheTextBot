using System;
using System.Threading.Tasks;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services.Configuration;
using Moq;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests;

[TestFixture]
public class ConversationProviderServiceTests
{
    [Test]
    public async Task DefaultProvider_RemainsDialogflow()
    {
        var provider = new Mock<IConversationService>();
        provider.Setup(value => value.GetResponseAsync(It.IsAny<Request>())).ReturnsAsync(new Response { Text = "legacy" });
        var router = new ConversationProviderService(new ConversationConfiguration(), () => provider.Object);
        var request = new Request { NewSession = true, ConversationState = "discard old state" };

        var result = await router.GetResponseAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.Text, Is.EqualTo("legacy"));
            Assert.That(result.ConversationProvider, Is.EqualTo("Dialogflow"));
            Assert.That(result.ConversationState, Is.Null);
        });
    }

    [TestCase(ConversationProvider.StoryDialog, "Dialogflow")]
    [TestCase(ConversationProvider.Dialogflow, "StoryDialog")]
    public async Task ProviderChangedMidSession_DoesNotResolveOtherProvider(ConversationProvider selected, string previous)
    {
        var resolved = false;
        var router = new ConversationProviderService(new ConversationConfiguration { Provider = selected }, () =>
        {
            resolved = true;
            throw new InvalidOperationException("Must not resolve another provider.");
        });
        var request = new Request { NewSession = false, ConversationProvider = previous, ConversationState = "compact", NextTextIndex = 7, RotationState = "legacy" };

        var result = await router.GetResponseAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.False);
            Assert.That(result.Text, Does.Contain("новую сессию"));
            Assert.That(result.ConversationProvider, Is.EqualTo(previous));
            Assert.That(result.ConversationState, Is.EqualTo("compact"));
            Assert.That(result.NextTextIndex, Is.EqualTo(7));
            Assert.That(result.RotationState, Is.EqualTo("legacy"));
        });
    }

    [Test]
    public async Task LegacyScopePinsDialogflowWithoutNewMetadata()
    {
        var router = new ConversationProviderService(new ConversationConfiguration { Provider = ConversationProvider.StoryDialog },
            () => throw new InvalidOperationException("Legacy session must not be moved."));

        var result = await router.GetResponseAsync(new Request { ScopeKey = "existing-agent", NewSession = false });

        Assert.That(result.ConversationProvider, Is.EqualTo("Dialogflow"));
    }

    [Test]
    public async Task NewSession_CanUseConfiguredProvider()
    {
        var provider = new Mock<IConversationService>();
        provider.Setup(value => value.GetResponseAsync(It.Is<Request>(request => request.ScopeKey == null && request.ConversationState == null)))
            .ReturnsAsync(new Response { Text = "new story", ConversationState = "new-state" });
        var router = new ConversationProviderService(new ConversationConfiguration { Provider = ConversationProvider.StoryDialog }, () => provider.Object);

        var result = await router.GetResponseAsync(new Request { NewSession = true, ConversationProvider = "Dialogflow", ConversationState = "old", ScopeKey = "old-agent" });

        Assert.That(result.ConversationProvider, Is.EqualTo("StoryDialog"));
        Assert.That(result.ConversationState, Is.EqualTo("new-state"));
    }

    [Test]
    public async Task UnknownOrOversizedPin_DoesNotEchoUnboundedState()
    {
        var router = new ConversationProviderService(new ConversationConfiguration(), () => throw new InvalidOperationException());
        var request = new Request { ConversationProvider = "unknown", ConversationState = new string('x', 800) };

        var result = await router.GetResponseAsync(request);

        Assert.That(result.ConversationProvider, Is.Null);
        Assert.That(result.ConversationState, Is.Null);
        Assert.That(request.ConversationState, Is.Null);
    }
}
