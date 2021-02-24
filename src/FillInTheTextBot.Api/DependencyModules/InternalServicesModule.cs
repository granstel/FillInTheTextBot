using Autofac;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using GranSteL.Helpers.Redis;
using StackExchange.Redis;

namespace FillInTheTextBot.Api.DependencyModules
{
    public class InternalServicesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ConversationService>().As<IConversationService>();
            builder.RegisterType<DialogflowService>().As<IDialogflowService>().InstancePerLifetimeScope();

            builder.Register(RegisterCacheService).As<IRedisCacheService>().SingleInstance();
        }

        private RedisCacheService RegisterCacheService(IComponentContext context)
        {
            var configuration = context.Resolve<RedisConfiguration>();

            var db = context.Resolve<IDatabase>();

            var service = new RedisCacheService(db, configuration.KeyPrefix);

            return service;
        }
    }
}
