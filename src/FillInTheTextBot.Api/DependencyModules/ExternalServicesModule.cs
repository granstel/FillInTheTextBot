using System;
using System.Linq;
using Autofac;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Dialogflow.V2;
using FillInTheTextBot.Services.Configuration;
using GranSteL.DialogflowBalancer;
using Grpc.Auth;
using RestSharp;
using StackExchange.Redis;

namespace FillInTheTextBot.Api.DependencyModules
{
    public class ExternalServicesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<RestClient>().As<IRestClient>();
            
            builder.Register(RegisterDialogflowBalancer).As<DialogflowClientsBalancer>().SingleInstance();

            builder.Register(RegisterRedisClient).As<IDatabase>().SingleInstance();
        }

        private DialogflowClientsBalancer RegisterDialogflowBalancer(IComponentContext context)
        {
            var configuration = context.Resolve<AppConfiguration>();

            var clientsConfigurations = configuration.DialogflowInstances
                .Select(i => new DialogflowClientsConfiguration(i.ProjectId, i.JsonPath))
                .ToList();

            var balancerConfiguration = new DialogflowBalancerConfiguration
            {
                ScopeExpiration = TimeSpan.FromMinutes(5),
                ClientsConfigurations = clientsConfigurations
            };

            var balancer = new DialogflowClientsBalancer(balancerConfiguration, RegisterDialogflowSessionsClient, RegisterDialogflowContextsClient);

            return balancer;
        }

        private SessionsClient RegisterDialogflowSessionsClient(DialogflowContext context)
        {
            var credential = GoogleCredential.FromFile(context.JsonPath).CreateScoped(SessionsClient.DefaultScopes);

            var clientBuilder = new SessionsClientBuilder
            {
                ChannelCredentials = credential.ToChannelCredentials()
            };

            var client = clientBuilder.Build();

            return client;
        }

        private ContextsClient RegisterDialogflowContextsClient(DialogflowContext context)
        {
            var credential = GoogleCredential.FromFile(context.JsonPath).CreateScoped(ContextsClient.DefaultScopes);

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
