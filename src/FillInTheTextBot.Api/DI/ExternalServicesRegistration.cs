using System;
using System.Collections.Generic;
using System.Linq;
using FillInTheTextBot.Services.Configuration;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Dialogflow.V2;
using GranSteL.Helpers.Redis;
using GranSteL.Tools.ScopeSelector;
using Grpc.Auth;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
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
                    context.TryAddParameter(nameof(configuration.EmulatorEndpoint), configuration.EmulatorEndpoint);

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
            if (TryGetEmulatorEndpoint(context, out var emulatorEndpoint))
            {
                var emulatorClientBuilder = new SessionsClientBuilder
                {
                    Endpoint = emulatorEndpoint,
                    ChannelCredentials = ChannelCredentials.Insecure
                };

                return emulatorClientBuilder.Build();
            }

            context.TryGetParameterValue(nameof(DialogflowConfiguration.JsonPath), out string jsonPath);
            var credential = LoadServiceAccountCredential(jsonPath, SessionsClient.DefaultScopes);

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
            if (TryGetEmulatorEndpoint(context, out var emulatorEndpoint))
            {
                var emulatorClientBuilder = new ContextsClientBuilder
                {
                    Endpoint = emulatorEndpoint,
                    ChannelCredentials = ChannelCredentials.Insecure
                };

                return emulatorClientBuilder.Build();
            }

            context.TryGetParameterValue(nameof(DialogflowConfiguration.JsonPath), out string jsonPath);
            var credential = LoadServiceAccountCredential(jsonPath, ContextsClient.DefaultScopes);

            var endpoint = GetEndpoint(context, ContextsClient.DefaultEndpoint);

            var clientBuilder = new ContextsClientBuilder
            {
                ChannelCredentials = credential.ToChannelCredentials(),
                Endpoint = endpoint
            };

            var client = clientBuilder.Build();

            return client;
        }

        /// <summary>
        /// GoogleCredential.FromFile объявлен устаревшим: он определяет тип учётных данных
        /// по содержимому файла, из-за чего подменённый файл может увести аутентификацию
        /// на другой механизм. CredentialFactory требует указать тип явно.
        /// </summary>
        private static GoogleCredential LoadServiceAccountCredential(string jsonPath, IEnumerable<string> scopes)
        {
            var serviceAccountCredential = CredentialFactory.FromFile<ServiceAccountCredential>(jsonPath);

            var credential = serviceAccountCredential.ToGoogleCredential().CreateScoped(scopes);

            return credential;
        }

        private static bool TryGetEmulatorEndpoint(ScopeContext context, out string endpoint)
        {
            context.TryGetParameterValue(nameof(DialogflowConfiguration.EmulatorEndpoint), out endpoint);

            return !string.IsNullOrWhiteSpace(endpoint);
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

        private static IRedisCacheService RegisterCacheService(IServiceProvider provider)
        {
            var configuration = provider.GetService<RedisConfiguration>();

            var db = provider.GetService<IDatabase>();

            var service = new RedisCacheService(db, configuration?.KeyPrefix);

            return service;
        }
    }
}
