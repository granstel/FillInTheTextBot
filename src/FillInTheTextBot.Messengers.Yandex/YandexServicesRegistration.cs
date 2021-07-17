using FillInTheTextBot.Services.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace FillInTheTextBot.Messengers.Yandex
{
    /// <summary>
    /// Probably, registered at DependencyConfiguration of main project
    /// </summary>
    public static class YandexServicesRegistration
    {
        internal static void AddYandexServices(this IServiceCollection services)
        {
            services.AddSingleton(context =>
            {
                var config = GetConfigurationFromFile<YandexConfiguration>("appsettings.Yandex.json");

                return config;
            });

            services.AddTransient<YandexController>();

            services.AddTransient<IYandexService, YandexService>();
            //TODO: is it really needed? Maybe registration of profiles at MappingRegistration enough
            services.AddTransient<YandexProfile>();
        }

        private const string Extension = ".json";

        private static T GetConfigurationFromFile<T>(string fileName) where T : MessengerConfiguration
        {
            if (fileName.IndexOf(Extension, StringComparison.InvariantCultureIgnoreCase) < 0)
            {
                fileName = $"{fileName}{Extension}";
            }

            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile(fileName, true, false);

            var configurationRoot = configurationBuilder.Build();

            var configuration = configurationRoot.Get<T>();

            return configuration;
        }
    }
}
