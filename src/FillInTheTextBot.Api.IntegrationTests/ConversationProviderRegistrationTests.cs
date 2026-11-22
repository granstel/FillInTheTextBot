using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.Rotation;
using FillInTheTextBot.Services.StoryDialog;
using Google.Cloud.Dialogflow.V2;
using GranSteL.Tools.ScopeSelector;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FillInTheTextBot.Api.IntegrationTests;

[TestFixture]
public class ConversationProviderRegistrationTests
{
    [Test]
    public void DefaultProvider_RegistersDialogflowAndRotationWithoutLoadingCredentials()
    {
        var builder = CreateBuilder();
        Program.ConfigureServices(builder.Services, builder.Configuration);

        using var host = builder.Build();
        using var scope = host.Services.CreateScope();

        Assert.Multiple(() =>
        {
            Assert.That(host.Services.GetRequiredService<ConversationConfiguration>().Provider,
                Is.EqualTo(ConversationProvider.Dialogflow));
            Assert.That(scope.ServiceProvider.GetRequiredService<IConversationService>(),
                Is.TypeOf<ConversationProviderService>());
            Assert.That(builder.Services.Any(service => service.ServiceType == typeof(ScopesSelector<SessionsClient>)), Is.True);
            Assert.That(builder.Services.Any(service => service.ServiceType == typeof(ScopesSelector<ContextsClient>)), Is.True);
            Assert.That(builder.Services.Any(service => service.ImplementationType == typeof(RotationRefreshService)), Is.True);
        });
    }

    [Test]
    public void StoryDialog_ResolvesWithInvalidGoogleCredentialPathAndWithoutRotation()
    {
        var builder = CreateBuilder("StoryDialog");
        Program.ConfigureServices(builder.Services, builder.Configuration);

        using var host = builder.Build();
        using var scope = host.Services.CreateScope();

        Assert.Multiple(() =>
        {
            Assert.That(scope.ServiceProvider.GetRequiredService<IConversationService>(),
                Is.TypeOf<ConversationProviderService>());
            Assert.That(host.Services.GetRequiredService<IStoryDialogClient>(), Is.TypeOf<GrpcStoryDialogClient>());
            Assert.That(builder.Services.Any(service => service.ServiceType == typeof(ScopesSelector<SessionsClient>)), Is.False);
            Assert.That(builder.Services.Any(service => service.ServiceType == typeof(ScopesSelector<ContextsClient>)), Is.False);
            Assert.That(builder.Services.Any(service => service.ServiceType == typeof(IRotationSource)), Is.False);
            Assert.That(builder.Services.Any(service => service.ImplementationType == typeof(RotationRefreshService)), Is.False);
        });
    }

    [TestCase("Unsupported")]
    [TestCase("999")]
    public void UnsupportedProvider_FailsConfiguration(string provider)
    {
        var builder = CreateBuilder(provider);

        Assert.Throws<InvalidOperationException>(() => Program.ConfigureServices(builder.Services, builder.Configuration));
    }

    private static HostApplicationBuilder CreateBuilder(string? provider = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development
        });
        builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        }));
        var settings = new Dictionary<string, string?>
        {
            ["AppConfiguration:HttpLog:Enabled"] = "false",
            ["AppConfiguration:Tracing:Enabled"] = "false",
            ["AppConfiguration:Redis:ConnectionString"] = "127.0.0.1:1",
            ["AppConfiguration:Dialogflow:0:ScopeId"] = "synthetic-scope",
            ["AppConfiguration:Dialogflow:0:ProjectId"] = "synthetic-project",
            ["AppConfiguration:Dialogflow:0:JsonPath"] = "synthetic-missing-credential.json",
            ["AppConfiguration:StoryDialog:Endpoint"] = "http://127.0.0.1:1",
            ["AppConfiguration:Rotation:Enabled"] = "true"
        };
        if (provider != null)
            settings["AppConfiguration:Conversation:Provider"] = provider;
        builder.Configuration.AddInMemoryCollection(settings);
        return builder;
    }
}
