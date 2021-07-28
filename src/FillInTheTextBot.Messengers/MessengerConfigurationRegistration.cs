using System;
using FillInTheTextBot.Services.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FillInTheTextBot.Messengers
{
    public static class MessengerConfigurationRegistration
    {
        public static void AddConfiguration<T>(this IServiceCollection services, string fileName) where T : MessengerConfiguration
        {
            services.AddSingleton(context =>
            {
                const string Extension = ".json";

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
            });
        }
    }
}
