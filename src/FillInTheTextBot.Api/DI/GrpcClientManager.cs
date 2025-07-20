using System;
using System.Collections.Concurrent;
using Google.Cloud.Dialogflow.V2;
using GranSteL.Tools.ScopeSelector;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Api.DI;

/// <summary>
/// Менеджер для управления gRPC клиентами и их жизненным циклом
/// </summary>
public class GrpcClientManager : IDisposable
{
    private readonly ILogger<GrpcClientManager> _logger;
    private readonly ConcurrentDictionary<string, SessionsClient> _sessionsClients = new();
    private readonly ConcurrentDictionary<string, ContextsClient> _contextsClients = new();
    private volatile bool _disposed;

    public GrpcClientManager(ILogger<GrpcClientManager> logger)
    {
        _logger = logger;
    }

    public SessionsClient GetOrCreateSessionsClient(ScopeContext context, Func<ScopeContext, SessionsClient> factory)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(GrpcClientManager));
        
        var key = context.ScopeId;
        return _sessionsClients.GetOrAdd(key, _ => 
        {
            _logger.LogInformation("Creating new SessionsClient for scope {ScopeId}", key);
            return factory(context);
        });
    }

    public ContextsClient GetOrCreateContextsClient(ScopeContext context, Func<ScopeContext, ContextsClient> factory)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(GrpcClientManager));
        
        var key = context.ScopeId;
        return _contextsClients.GetOrAdd(key, _ => 
        {
            _logger.LogInformation("Creating new ContextsClient for scope {ScopeId}", key);
            return factory(context);
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _logger.LogInformation("Disposing GrpcClientManager - cleaning up {SessionsCount} SessionsClients and {ContextsCount} ContextsClients", 
            _sessionsClients.Count, _contextsClients.Count);

        foreach (var client in _sessionsClients.Values)
        {
            try
            {
                // Попробуем вызвать Dispose или CloseAsync если доступно
                if (client is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                else if (client is IAsyncDisposable asyncDisposable)
                {
                    asyncDisposable.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing SessionsClient");
            }
        }

        foreach (var client in _contextsClients.Values)
        {
            try
            {
                if (client is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                else if (client is IAsyncDisposable asyncDisposable)
                {
                    asyncDisposable.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing ContextsClient");
            }
        }

        _sessionsClients.Clear();
        _contextsClients.Clear();
        _disposed = true;
        
        _logger.LogInformation("GrpcClientManager disposed successfully");
    }
}