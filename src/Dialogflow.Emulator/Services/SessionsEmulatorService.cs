using Dialogflow.Emulator.Models;
using Google.Cloud.Dialogflow.V2;
using Grpc.Core;

namespace Dialogflow.Emulator.Services;

/// <summary>
/// Стаб DetectIntent. Настоящего разбора естественного языка нет: интент ищется
/// по имени события, затем по точному совпадению с обучающей фразой из выгрузки
/// агента, иначе отдаётся Default Fallback Intent. Контексты и слот-филлинг
/// не поддерживаются.
/// </summary>
public sealed class SessionsEmulatorService : Sessions.SessionsBase
{
    private readonly IAgentStorage _agentStorage;
    private readonly ILogger<SessionsEmulatorService> _log;

    public SessionsEmulatorService(IAgentStorage agentStorage, ILogger<SessionsEmulatorService> log)
    {
        _agentStorage = agentStorage;
        _log = log;
    }

    public override Task<DetectIntentResponse> DetectIntent(DetectIntentRequest request, ServerCallContext context)
    {
        var queryText = string.Empty;

        AgentIntent? intent = null;

        if (request.QueryInput?.Event is not null)
        {
            queryText = $"event:{request.QueryInput.Event.Name}";

            intent = _agentStorage.FindByEvent(request.QueryInput.Event.Name);
        }
        else if (request.QueryInput?.Text is not null)
        {
            queryText = request.QueryInput.Text.Text;

            intent = _agentStorage.FindByText(queryText);
        }

        intent ??= _agentStorage.GetFallback();

        _log.LogInformation("DetectIntent '{QueryText}' matched intent '{IntentName}'", queryText, intent?.Name);

        var response = CreateResponse(intent, queryText, request.Session);

        return Task.FromResult(response);
    }

    private static DetectIntentResponse CreateResponse(AgentIntent? intent, string queryText, string session)
    {
        var fulfillmentText = AgentStorage.GetText(intent) ?? string.Empty;

        var queryResult = new QueryResult
        {
            QueryText = queryText,
            FulfillmentText = fulfillmentText,
            Action = AgentStorage.GetAction(intent) ?? string.Empty,
            AllRequiredParamsPresent = true,
            IntentDetectionConfidence = 1f,
            LanguageCode = "ru",
            Intent = new Intent
            {
                DisplayName = intent?.Name ?? string.Empty,
                Name = $"{session}/intents/{intent?.Id ?? string.Empty}"
            }
        };

        queryResult.FulfillmentMessages.Add(new Intent.Types.Message
        {
            Text = new Intent.Types.Message.Types.Text { Text_ = { fulfillmentText } }
        });

        var response = new DetectIntentResponse
        {
            ResponseId = Guid.NewGuid().ToString("N"),
            QueryResult = queryResult
        };

        return response;
    }
}
