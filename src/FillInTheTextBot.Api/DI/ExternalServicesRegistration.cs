using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        }

        private static IEnumerable<ScopeContext> GetScopesContexts(IEnumerable<DialogflowConfiguration> dialogflowConfigurations)
        {
            var scopeContexts = dialogflowConfigurations
                .Where(configuration => !string.IsNullOrEmpty(configuration.ScopeId))
                .Select(configuration =>
                {
                    var context = new ScopeContext(configuration.ScopeId, configuration.DoNotUseForNewSessions);

                    context.TryAddParameter(nameof(configuration.ProjectId), configuration.ProjectId);
                    context.TryAddParameter(nameof(configuration.JsonPath), configuration.JsonPath);
                    context.TryAddParameter(nameof(configuration.Region), configuration.Region);
                    context.TryAddParameter(nameof(configuration.LanguageCode), configuration.LanguageCode);
                    context.TryAddParameter(nameof(configuration.LogQuery), configuration.LogQuery.ToString());

                    return context;
                });

            return scopeContexts;
        }

        private static ScopesSelector<SessionsClient> RegisterSessionsClientScopes(IServiceProvider provider)
        {
            var configuration = provider.GetService<DialogflowConfiguration[]>();

            var scopeContexts = GetScopesContexts(configuration);

            var selector = new ScopesSelector<SessionsClient>(scopeContexts, CreateDialogflowSessionsClient);

            return selector;
        }

        private static SessionsClient CreateDialogflowSessionsClient(ScopeContext context)
        {
            context.TryGetParameterValue(nameof(DialogflowConfiguration.JsonPath), out string jsonPath);
            var credential = GoogleCredential.FromFile(jsonPath).CreateScoped(SessionsClient.DefaultScopes);

            var endpoint = GetEndpoint(context, SessionsClient.DefaultEndpoint);

            var clientBuilder = new SessionsClientBuilder
            {
                ChannelCredentials = credential.ToChannelCredentials(),
                Endpoint = endpoint
            };

            var client = clientBuilder.Build();

            return client;
        }

        private static ScopesSelector<ContextsClient> RegisterContextsClientScopes(IServiceProvider provider)
        {
            var configuration = provider.GetService<DialogflowConfiguration[]>();

            var contexts = GetScopesContexts(configuration);

            var selector = new ScopesSelector<ContextsClient>(contexts, CreateDialogflowContextsClient);

            return selector;
        }

        private static ContextsClient CreateDialogflowContextsClient(ScopeContext context)
        {
            context.TryGetParameterValue(nameof(DialogflowConfiguration.JsonPath), out string jsonPath);
            var credential = GoogleCredential.FromFile(jsonPath).CreateScoped(ContextsClient.DefaultScopes);

            var endpoint = GetEndpoint(context, SessionsClient.DefaultEndpoint);

            var clientBuilder = new ContextsClientBuilder
            {
                ChannelCredentials = credential.ToChannelCredentials(),
                Endpoint = endpoint
            };

            var client = clientBuilder.Build();

            return client;
        }

        private static string GetEndpoint(ScopeContext context, string defaultEndpoint)
        {
            context.TryGetParameterValue(nameof(DialogflowConfiguration.Region), out string region);

            if (string.IsNullOrWhiteSpace(region))
            {
                return defaultEndpoint;
            }

            return $"{region}-{defaultEndpoint}";
        }

        private static IDatabase RegisterRedisClient(IServiceProvider provider)
        {
            // TODO: get config as parameter
            var configuration = provider.GetService<RedisConfiguration>();

            var redisClient = ConnectionMultiplexer.Connect(configuration.ConnectionString);

            var dataBase = redisClient.GetDatabase();

            return dataBase;
        }

        private static ITracer RegisterTracer(IServiceProvider provider)
        {
            var env = provider.GetService<IWebHostEnvironment>();
            // TODO: get config as parameter
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
