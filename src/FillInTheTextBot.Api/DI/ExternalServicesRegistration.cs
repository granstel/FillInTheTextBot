using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Dialogflow.V2;
using GranSteL.Helpers.Redis;
using GranSteL.Tools.ScopeSelector;
using Grpc.Auth;
using Jaeger;
using Jaeger.Reporters;
using Jaeger.Samplers;
using Jaeger.Senders.Thrift;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OpenTracing;
using OpenTracing.Util;
using StackExchange.Redis;

namespace FillInTheTextBot.Api.DI
{
    internal static class ExternalServicesRegistration
    {
        internal static void AddExternalServices(this IServiceCollection services)
        {
            services.AddSingleton(RegisterSessionsClientScopes);
            services.AddSingleton(RegisterContextsClientScopes);
            services.AddSingleton(RegisterRedisClient);
            services.AddSingleton(RegisterTracer);
            services.AddSingleton(RegisterCacheService);

            services.AddSingleton<IScopeBindingStorage, ScopeBindingStorage>();
        }

        private static ScopesSelector<SessionsClient> RegisterSessionsClientScopes(IServiceProvider provider)
        {
            var configuration = provider.GetService<DialogflowConfiguration[]>();

            var scopeContexts = GetScopesContexts(configuration);

            var storage = provider.GetService<IScopeBindingStorage>();
            var balancer = new ScopesSelector<SessionsClient>(storage, scopeContexts, CreateDialogflowSessionsClient);

            return balancer;
        }

        private static SessionsClient CreateDialogflowSessionsClient(ScopeContext context)
        {
            var credential = GoogleCredential.FromFile(context.Parameters["JsonPath"]).CreateScoped(SessionsClient.DefaultScopes);

            var clientBuilder = new SessionsClientBuilder
            {
                ChannelCredentials = credential.ToChannelCredentials()
            };

            var client = clientBuilder.Build();

            return client;
        }

        private static ScopesSelector<ContextsClient> RegisterContextsClientScopes(IServiceProvider provider)
        {
            var configuration = provider.GetService<DialogflowConfiguration[]>();

            var contexts = GetScopesContexts(configuration);

            var storage = provider.GetService<IScopeBindingStorage>();
            var balancer = new ScopesSelector<ContextsClient>(storage, contexts, CreateDialogflowContextsClient);
            
            return balancer;
        }
        
        private static ContextsClient CreateDialogflowContextsClient(ScopeContext context)
        {
            var credential = GoogleCredential.FromFile(context.Parameters["JsonPath"]).CreateScoped(ContextsClient.DefaultScopes);

            var clientBuilder = new ContextsClientBuilder
            {
                ChannelCredentials = credential.ToChannelCredentials()
            };

            var client = clientBuilder.Build();

            return client;
        }

        private static ICollection<ScopeContext> GetScopesContexts(DialogflowConfiguration[] dialogflowScopes)
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

        private static IDatabase RegisterRedisClient(IServiceProvider provider)
        {
            var configuration = provider.GetService<RedisConfiguration>();

            var redisClient = ConnectionMultiplexer.Connect(configuration.ConnectionString);

            var dataBase = redisClient.GetDatabase();

            return dataBase;
        }

        private static ITracer RegisterTracer(IServiceProvider provider)
        {
            var env = provider.GetService<IWebHostEnvironment>();
            var configuration = provider.GetService<TracingConfiguration>();

            var serviceName = env.ApplicationName;
            var fullVersion = Assembly.GetExecutingAssembly().GetName().Version;

            var version = $"{fullVersion?.Major}.{fullVersion?.Minor}.{fullVersion?.Build}";

            var sampler = new ConstSampler(true);
            var reporter = new RemoteReporter.Builder()
                .WithSender(new UdpSender(configuration.Host, configuration.Port, 0))
                .Build();

            var tracer = new Tracer.Builder(serviceName)
                .WithSampler(sampler)
                .WithReporter(reporter)
                .WithTag("Version", version)
                .Build();

            GlobalTracer.Register(tracer);
            return tracer;
        }
        
        private static IRedisCacheService RegisterCacheService(IServiceProvider provider)
        {
            var configuration = provider.GetService<RedisConfiguration>();

            var db = provider.GetService<IDatabase>();

            var service = new RedisCacheService(db, configuration?.KeyPrefix);

            return service;
        }
    }
}
