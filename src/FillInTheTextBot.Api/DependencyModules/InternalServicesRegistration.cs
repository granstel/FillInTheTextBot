using System;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using GranSteL.Helpers.Redis;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace FillInTheTextBot.Api.DependencyModules
{
    internal static class InternalServicesRegistration
    {
        internal static void AddInternalServices(this IServiceCollection services)
        {
            services.AddTransient<IConversationService, ConversationService>();
            services.AddScoped<IDialogflowService, DialogflowService>();

            services.AddSingleton(RegisterCacheService);
        }

        private static IRedisCacheService RegisterCacheService(IServiceProvider provider)
        {
            var configuration = provider.GetService<RedisConfiguration>();

            var db = provider.GetService<IDatabase>();

            var service = new RedisCacheService(db, configuration.KeyPrefix);

            return service;
        }
    }
}
