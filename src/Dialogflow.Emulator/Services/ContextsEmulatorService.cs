using Google.Cloud.Dialogflow.V2;
using Grpc.Core;

namespace Dialogflow.Emulator.Services;

/// <summary>
/// Стаб работы с контекстами. Контексты нигде не хранятся и на подбор интента
/// не влияют — сервис нужен, чтобы вызовы CreateContext не падали.
/// </summary>
public sealed class ContextsEmulatorService : Contexts.ContextsBase
{
    private readonly ILogger<ContextsEmulatorService> _log;

    public ContextsEmulatorService(ILogger<ContextsEmulatorService> log)
    {
        _log = log;
    }

    public override Task<Context> CreateContext(CreateContextRequest request, ServerCallContext context)
    {
        _log.LogInformation("CreateContext '{ContextName}'", request.Context?.Name);

        return Task.FromResult(request.Context ?? new Context());
    }
}
