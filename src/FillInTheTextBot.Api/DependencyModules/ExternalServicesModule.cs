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
            
            builder.Register(RegisterDialogflowSessionsClient).As<SessionsClient>().SingleInstance();
            builder.Register(RegisterDialogflowContextsClient).As<ContextsClient>().SingleInstance();

            builder.Register(RegisterDialogflowBalancer).As<DialogflowClientsBalancer>().SingleInstance();

            builder.Register(RegisterRedisClient).As<IDatabase>().SingleInstance();
        }

        private DialogflowClientsBalancer RegisterDialogflowBalancer(IComponentContext context)
        {
            var configuration = context.Resolve<AppConfiguration>();

            var balancer = new DialogflowClientsBalancer()
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

            var credential = GoogleCredential.FromFile(configuration.JsonPath).CreateScoped(ContextsClient.DefaultScopes);

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
