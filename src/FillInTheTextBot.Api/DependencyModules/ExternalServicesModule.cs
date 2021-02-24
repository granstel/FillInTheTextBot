using System.Collections.Generic;
using System.Linq;
using Autofac;
using FillInTheTextBot.Services;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Dialogflow.V2;
using FillInTheTextBot.Services.Configuration;
using GranSteL.Tools.ScopeSelector;
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
            
            builder.Register(RegisterSessionsClientBalancer).As<ScopesSelector<SessionsClient>>().SingleInstance();
            builder.Register(RegisterContextsClientBalancer).As<ScopesSelector<ContextsClient>>().SingleInstance();
            builder.Register(RegisterRedisClient).As<IDatabase>().SingleInstance();
            builder.RegisterType<ScopesStorage>().As<IScopesStorage>().InstancePerLifetimeScope();
        }

        private ScopesSelector<SessionsClient> RegisterSessionsClientBalancer(IComponentContext context)
        {
            var configuration = context.Resolve<AppConfiguration>();

            var scopeContexts = GetScopesContexts(configuration);

            var storage = context.Resolve<IScopesStorage>();
            var balancer = new ScopesSelector<SessionsClient>(storage, scopeContexts, CreateDialogflowSessionsClient);
            
            return balancer;
        }

        private SessionsClient CreateDialogflowSessionsClient(ScopeContext context)
        {
            var credential = GoogleCredential.FromFile(context.Parameters["JsonPath"]).CreateScoped(SessionsClient.DefaultScopes);

            var clientBuilder = new SessionsClientBuilder
            {
                ChannelCredentials = credential.ToChannelCredentials()
            };

            var client = clientBuilder.Build();

            return client;
        }

        private ScopesSelector<ContextsClient> RegisterContextsClientBalancer(IComponentContext context)
        {
            var configuration = context.Resolve<AppConfiguration>();

            var contexts = GetScopesContexts(configuration);

            var storage = context.Resolve<IScopesStorage>();
            var balancer = new ScopesSelector<ContextsClient>(storage, contexts, CreateDialogflowContextsClient);
            
            return balancer;
        }
        
        private ContextsClient CreateDialogflowContextsClient(ScopeContext context)
        {
            var credential = GoogleCredential.FromFile(context.Parameters["JsonPath"]).CreateScoped(ContextsClient.DefaultScopes);

            var clientBuilder = new ContextsClientBuilder
            {
                ChannelCredentials = credential.ToChannelCredentials()
            };

            var client = clientBuilder.Build();

            return client;
        }

        private ICollection<ScopeContext> GetScopesContexts(AppConfiguration configuration)
        {
            var scopeContexts = configuration.DialogflowInstances
                .Select(i =>
                {
                    var context = new ScopeContext(i.ProjectId);
                    
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
