using FillInTheTextBot.Services.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FillInTheTextBot.Api.DI
{
    internal static class ConfigurationRegistration
    {
        internal static void AddAppConfiguration(this IServiceCollection services, AppConfiguration configuration)
        {
            configuration.Conversation ??= new ConversationConfiguration();
            configuration.StoryDialog ??= new StoryDialogConfiguration();
            configuration.Dialogflow ??= System.Array.Empty<DialogflowConfiguration>();
            if (!System.Enum.IsDefined(configuration.Conversation.Provider))
            {
                throw new System.InvalidOperationException("Unsupported conversation provider.");
            }
            services.AddSingleton(configuration);
            services.AddSingleton(configuration.HttpLog);
            services.AddSingleton(configuration.Redis);
            services.AddSingleton(configuration.Dialogflow);
            services.AddSingleton(configuration.Tracing);
            services.AddSingleton(configuration.Conversation);
            services.AddSingleton(configuration.StoryDialog);
            services.AddSingleton(configuration.Shutdown ?? new ShutdownConfiguration());
            services.AddSingleton(configuration.Rotation ?? new RotationConfiguration());
        }
    }
}
