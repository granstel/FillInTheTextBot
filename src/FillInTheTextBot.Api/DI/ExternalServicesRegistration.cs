using System;
using System.Collections.Generic;
using System.Linq;
using FillInTheTextBot.Services.Configuration;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Dialogflow.V2;
using GranSteL.Helpers.Redis;
using GranSteL.Tools.ScopeSelector;
using Grpc.Auth;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace FillInTheTextBot.Api.DI;

internal static class ExternalServicesRegistration
{
    internal static void AddExternalServices(this IServiceCollection services)
    {
        // Регистрируем менеджер gRPC клиентов как Singleton для правильного управления жизненным циклом
        services.AddSingleton<GrpcClientManager>();
        
        services.AddSingleton(RegisterSessionsClientScopes);
        services.AddSingleton(RegisterContextsClientScopes);
        services.AddSingleton(RegisterRedisConnectionMultiplexer);
        services.AddSingleton(RegisterRedisClient);
        services.AddSingleton(RegisterCacheService);
    }

    private static IEnumerable<ScopeContext> GetScopesContexts(
        IEnumerable<DialogflowConfiguration> dialogflowConfigurations)
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
        var grpcManager = provider.GetService<GrpcClientManager>();

        var scopeContexts = GetScopesContexts(configuration);

        var selector = new ScopesSelector<SessionsClient>(scopeContexts, 
            context => grpcManager.GetOrCreateSessionsClient(context, CreateDialogflowSessionsClient));

        return selector;
    }

    private static SessionsClient CreateDialogflowSessionsClient(ScopeContext context)
    {
        context.TryGetParameterValue(nameof(DialogflowConfiguration.JsonPath), out var jsonPath);
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
        var grpcManager = provider.GetService<GrpcClientManager>();

        var contexts = GetScopesContexts(configuration);

        var selector = new ScopesSelector<ContextsClient>(contexts, 
            context => grpcManager.GetOrCreateContextsClient(context, CreateDialogflowContextsClient));

        return selector;
    }

    private static ContextsClient CreateDialogflowContextsClient(ScopeContext context)
    {
        context.TryGetParameterValue(nameof(DialogflowConfiguration.JsonPath), out var jsonPath);
        var credential = GoogleCredential.FromFile(jsonPath).CreateScoped(ContextsClient.DefaultScopes);

        var endpoint = GetEndpoint(context, ContextsClient.DefaultEndpoint);

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
        context.TryGetParameterValue(nameof(DialogflowConfiguration.Region), out var region);

        if (string.IsNullOrWhiteSpace(region)) return defaultEndpoint;

        return $"{region}-{defaultEndpoint}";
    }

    private static IConnectionMultiplexer RegisterRedisConnectionMultiplexer(IServiceProvider provider)
    {
        var configuration = provider.GetService<RedisConfiguration>();
        return ConnectionMultiplexer.Connect(configuration.ConnectionString);
    }

    private static IDatabase RegisterRedisClient(IServiceProvider provider)
    {
        var redisClient = provider.GetService<IConnectionMultiplexer>();
        return redisClient.GetDatabase();
    }

    private static IRedisCacheService RegisterCacheService(IServiceProvider provider)
    {
        var configuration = provider.GetService<RedisConfiguration>();

        var db = provider.GetService<IDatabase>();

        var service = new RedisCacheService(db, configuration?.KeyPrefix);

        return service;
    }
}