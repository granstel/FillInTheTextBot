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

            var balancer = new ScopesSelector<SessionsClient>(scopeContexts, CreateDialogflowSessionsClient);

            return balancer;
        }

        private static SessionsClient CreateDialogflowSessionsClient(ScopeContext context)
        {
            var credential = GoogleCredential.FromFile(context.Parameters["JsonPath"]).CreateScoped(SessionsClient.DefaultScopes);

            var clientBuilder = new SessionsClientBuilder
            {
                ChannelCredentials = credential.ToChannelCredentials(),
                Endpoint = $"europe-west1-{ContextsClient.DefaultEndpoint}"
            };

            var client = clientBuilder.Build();

            return client;
        }

        private static ScopesSelector<ContextsClient> RegisterContextsClientScopes(IServiceProvider provider)
        {
            var configuration = provider.GetService<DialogflowConfiguration[]>();

            var contexts = GetScopesContexts(configuration);

            var balancer = new ScopesSelector<ContextsClient>(contexts, CreateDialogflowContextsClient);
            
            return balancer;
        }
        
        private static ContextsClient CreateDialogflowContextsClient(ScopeContext context)
        {
            var credential = GoogleCredential.FromFile(context.Parameters["JsonPath"]).CreateScoped(SessionsClient.DefaultScopes);

            var clientBuilder = new ContextsClientBuilder
            {
                ChannelCredentials = credential.ToChannelCredentials(),
                Endpoint = $"europe-west1-{ContextsClient.DefaultEndpoint}"
            };

            var client = clientBuilder.Build();

            return client;
        }

        private static ICollection<ScopeContext> GetScopesContexts(DialogflowConfiguration[] dialogflowConfigurations)
        {
            var scopeContexts = dialogflowConfigurations.Where(s => !string.IsNullOrEmpty(s.ProjectId))
                .Select(s =>
                {
                    var context = new ScopeContext(s.ProjectId, s.DoNotAddToQueue);
                    
                    context.Parameters.Add("ProjectId", s.ProjectId);
                    context.Parameters.Add("JsonPath", s.JsonPath);
                    context.Parameters.Add("LogQuery", s.LogQuery.ToString());
                    context.Parameters.Add("LanguageCode", s.LanguageCode);

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
