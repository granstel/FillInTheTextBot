using FillInTheTextBot.Messengers.Yandex;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FillInTheTextBot.Api.IntegrationTests;

[TestFixture]
public class YandexConfigurationTests
{
    [TestCase("")]
    [TestCase("synthetic-incoming-token")]
    public void BindsIncomingTokenFromApplicationConfiguration(string token)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Yandex:IncomingToken"] = token }).Build();
        var services = new ServiceCollection();
        services.AddYandex(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.That(provider.GetRequiredService<YandexConfiguration>().IncomingToken, Is.EqualTo(token));
    }
}
