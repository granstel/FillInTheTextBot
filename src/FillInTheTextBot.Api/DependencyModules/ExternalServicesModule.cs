using System.Collections.Generic;
using System.Linq;
using Autofac;
using FillInTheTextBot.Services;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Dialogflow.V2;
using FillInTheTextBot.Services.Configuration;
using GranSteL.Tools.ScopeSelector;
using Grpc.Auth;
using Jaeger;
using Jaeger.Reporters;
using Jaeger.Samplers;
using Jaeger.Senders.Thrift;
using Microsoft.AspNetCore.Hosting;
using OpenTracing;
using OpenTracing.Util;
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
            builder.RegisterType<ScopeBindingStorage>().As<IScopeBindingStorage>().InstancePerLifetimeScope();
            builder.Register(RegisterTracer).As<ITracer>().SingleInstance();
        }

        private ScopesSelector<SessionsClient> RegisterSessionsClientBalancer(IComponentContext context)
        {
            var configuration = context.Resolve<DialogflowConfiguration[]>();

            var scopeContexts = GetScopesContexts(configuration);

            var storage = context.Resolve<IScopeBindingStorage>();
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
            var configuration = context.Resolve<DialogflowConfiguration[]>();

            var contexts = GetScopesContexts(configuration);

            var storage = context.Resolve<IScopeBindingStorage>();
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

        private ICollection<ScopeContext> GetScopesContexts(DialogflowConfiguration[] dialogflowScopes)
        {
            var scopeContexts = dialogflowScopes.Where(i => !string.IsNullOrEmpty(i.ProjectId))
                .Select(i =>
                {
                    var context = new ScopeContext(i.ProjectId);
                    
                    context.Parameters.Add("ProjectId", i.ProjectId);
                    context.Parameters.Add("JsonPath", i.JsonPath);
                    context.Parameters.Add("LogQuery", i.LogQuery.ToString());
                    context.Parameters.Add("LanguageCode", i.LanguageCode);

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

        private ITracer RegisterTracer(IComponentContext context)
        {
            var env = context.Resolve<IWebHostEnvironment>();
            var configuration = context.Resolve<TracingConfiguration>();

            var serviceName = env.ApplicationName;

            var sampler = new ConstSampler(true);
            var reporter = new RemoteReporter.Builder()
                .WithSender(new UdpSender(configuration.Host, configuration.Port, 0))
                .Build();

            var tracer = new Tracer.Builder(serviceName)
                .WithSampler(sampler)
                .WithReporter(reporter)
                .Build();

            GlobalTracer.Register(tracer);
            return tracer;
        }
    }
}
