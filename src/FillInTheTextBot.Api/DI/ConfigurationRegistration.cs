using FillInTheTextBot.Services.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FillInTheTextBot.Api.DI
{
    internal static class ConfigurationRegistration
    {
        internal static void AddAppConfiguration(this IServiceCollection services, IConfiguration appConfiguration)
        {
            var configuration = appConfiguration.GetSection($"{nameof(AppConfiguration)}").Get<AppConfiguration>();

            services.AddSingleton(configuration);
            services.AddSingleton(configuration.HttpLog);
            services.AddSingleton(configuration.Redis);
            services.AddSingleton(configuration.DialogflowScopes);
            services.AddSingleton(configuration.Tracing);
            services.AddSingleton(configuration.ConversationConfiguration);
        }
    }
}
