using System.Collections.Generic;
using System.Linq;
using Autofac;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Dialogflow.V2;
using FillInTheTextBot.Services.Configuration;
using GranSteL.ScopesBalancer;
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
            
            builder.Register(RegisterSessionsClientBalancer).As<ScopesBalancer<SessionsClient>>().SingleInstance();
            builder.Register(RegisterContextsClientBalancer).As<ScopesBalancer<ContextsClient>>().SingleInstance();
            builder.Register(RegisterRedisClient).As<IDatabase>().SingleInstance();
        }

        private ScopesBalancer<SessionsClient> RegisterSessionsClientBalancer(IComponentContext context)
        {
            var configuration = context.Resolve<AppConfiguration>();

            var scopeContexts = GetScopesContexts<SessionsClient>(configuration);

            var storage = context.Resolve<IScopesStorage>();
            var balancer = new ScopesBalancer<SessionsClient>(storage, scopeContexts, CreateDialogflowSessionsClient);
            
            return balancer;
        }

        private SessionsClient CreateDialogflowSessionsClient(ScopeContext<SessionsClient> context)
        {
            var credential = GoogleCredential.FromFile(context.Parameters["JsonPath"]).CreateScoped(SessionsClient.DefaultScopes);

            var clientBuilder = new SessionsClientBuilder
            {
                ChannelCredentials = credential.ToChannelCredentials()
            };

            var client = clientBuilder.Build();

            return client;
        }

        private ScopesBalancer<ContextsClient> RegisterContextsClientBalancer(IComponentContext context)
        {
            var configuration = context.Resolve<AppConfiguration>();

            var contexts = GetScopesContexts<ContextsClient>(configuration);

            var storage = context.Resolve<IScopesStorage>();
            var balancer = new ScopesBalancer<ContextsClient>(storage, contexts, CreateDialogflowContextsClient);
            
            return balancer;
        }
        
        private ContextsClient CreateDialogflowContextsClient(ScopeContext<ContextsClient> context)
        {
            var credential = GoogleCredential.FromFile(context.Parameters["JsonPath"]).CreateScoped(ContextsClient.DefaultScopes);

            var clientBuilder = new ContextsClientBuilder
            {
                ChannelCredentials = credential.ToChannelCredentials()
            };

            var client = clientBuilder.Build();

            return client;
        }

        private ICollection<ScopeContext<T>> GetScopesContexts<T>(AppConfiguration configuration)
        {
            var scopeContexts = configuration.DialogflowInstances
                .Select(i =>
                {
                    var context = new ScopeContext<T>(i.ProjectId);
                    
                    context.Parameters.Add("ProjectId", i.ProjectId);
                    context.Parameters.Add("JsonPath", i.JsonPath);

                    return context;
                })
                .ToList();

            return scopeContexts;
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
