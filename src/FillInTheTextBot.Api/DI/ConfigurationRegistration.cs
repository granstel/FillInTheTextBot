using FillInTheTextBot.Services.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FillInTheTextBot.Api.DI
{
    internal static class ConfigurationRegistration
    {
        internal static void AddAppConfiguration(this IServiceCollection services, AppConfiguration configuration)
        {
            services.AddSingleton(configuration);
            services.AddSingleton(configuration.HttpLog);
            services.AddSingleton(configuration.Redis);
            services.AddSingleton(configuration.Dialogflow);
            services.AddSingleton(configuration.Tracing);
            services.AddSingleton(configuration.Conversation);
        }
    }
}
