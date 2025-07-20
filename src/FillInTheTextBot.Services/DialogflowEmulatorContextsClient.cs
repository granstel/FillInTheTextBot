using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Dialogflow.V2;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Services;

/// <summary>
/// HTTP клиент для эмулятора Dialogflow контекстов
/// </summary>
public class DialogflowEmulatorContextsClient : ContextsClient
{
    private readonly string _baseUrl;
    private readonly ILogger<DialogflowEmulatorContextsClient> _logger;

    public DialogflowEmulatorContextsClient(string baseUrl, ILogger<DialogflowEmulatorContextsClient> logger = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _logger = logger;
    }

    public override Task<Context> CreateContextAsync(SessionName parent, Context context, CancellationToken cancellationToken = default)
    {
        // Для эмулятора просто возвращаем тот же контекст
        // В реальной реализации здесь был бы HTTP вызов к эмулятору
        _logger?.LogTrace($"Creating context {context.ContextName} for session {parent.SessionId}");
        
        return Task.FromResult(context);
    }

    public override Task<Context> CreateContextAsync(CreateContextRequest request, CancellationToken cancellationToken = default)
    {
        return CreateContextAsync(request.ParentAsSessionName, request.Context, cancellationToken);
    }

    // No resources to dispose
}