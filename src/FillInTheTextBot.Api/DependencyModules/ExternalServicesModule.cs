﻿using Autofac;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Dialogflow.V2;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Clients;
using FillInTheTextBot.Services.Configuration;
using Grpc.Auth;
using RestSharp;
using StackExchange.Redis;
using System;

namespace FillInTheTextBot.Api.DependencyModules
{
    public class ExternalServicesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<RestClient>().As<IRestClient>();
            
            builder.RegisterType<QnaClient>().As<IQnaClient>();
            
            builder.Register(RegisterDialogflowSessionsClient).As<SessionsClient>();
            builder.Register(RegisterDialogflowContextsClient).As<ContextsClient>();

            builder.Register(RegisterRedisClient).As<IDatabase>().SingleInstance();
        }

        private SessionsClient RegisterDialogflowSessionsClient(IComponentContext context)
        {
            var configuration = context.Resolve<DialogflowConfiguration>();

            var credential = GoogleCredential.FromFile(configuration.JsonPath).CreateScoped(SessionsClient.DefaultScopes);

            var clientBuilder = new SessionsClientBuilder
            {
                ChannelCredentials = credential.ToChannelCredentials()
            };

            var client = clientBuilder.Build();

            return client;
        }

        private ContextsClient RegisterDialogflowContextsClient(IComponentContext context)
        {
            var configuration = context.Resolve<DialogflowConfiguration>();

            var credential = GoogleCredential.FromFile(configuration.JsonPath).CreateScoped(SessionsClient.DefaultScopes);

            var clientBuilder = new ContextsClientBuilder
            {
                ChannelCredentials = credential.ToChannelCredentials()
            };

            var client = clientBuilder.Build();

            return client;
        }

        private IDatabase RegisterRedisClient(IComponentContext context)
        {
            var configuration = context.Resolve<RedisConfiguration>();

            var redisClient = ConnectionMultiplexer.Connect(configuration.ConnectionString);

            var dataBase = redisClient.GetDatabase();

            return dataBase;
        }
    }
}
