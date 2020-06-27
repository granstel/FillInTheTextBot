﻿using Autofac;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.Serialization;
using GranSteL.Helpers.Redis;
using StackExchange.Redis;

namespace FillInTheTextBot.Api.DependencyModules
{
    public class InternalServicesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ConversationService>().As<IConversationService>();
            builder.RegisterType<QnaService>().As<IQnaService>();
            builder.RegisterType<DialogflowService>().As<IDialogflowService>();
            builder.RegisterType<CustomJsonSerializer>().AsSelf();

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
