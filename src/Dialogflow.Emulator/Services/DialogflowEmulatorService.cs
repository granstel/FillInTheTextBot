namespace Dialogflow.Emulator.Services;

using Google.Cloud.Dialogflow.V2;
using Grpc.Core;
using static Google.Cloud.Dialogflow.V2.Sessions;

public class DialogflowEmulatorService(
    ILogger<DialogflowEmulatorService> logger,
    IAgentStorage agentStorage,
    IIntentMatcher intentMatcher) : SessionsBase
{
    public override Task<DetectIntentResponse> DetectIntent(DetectIntentRequest request, ServerCallContext context)
    {
        logger.LogInformation("DetectIntent request for session: {Session}", request.Session);

        Models.Intent? matchedIntent = null;
        var queryText = "";

        if (request.QueryInput.Event != null)
        {
            queryText = $"event:{request.QueryInput.Event.Name}";
            matchedIntent = agentStorage.FindIntentByEvent(request.QueryInput.Event.Name);
        }
        else if (request.QueryInput.Text != null)
        {
            queryText = request.QueryInput.Text.Text;
            matchedIntent = intentMatcher.Match(queryText);
        }

        matchedIntent ??= agentStorage.GetIntent("Default Fallback Intent");

        var response = CreateDetectIntentResponse(matchedIntent, queryText, request.Session);
        return Task.FromResult(response);
    }

    private DetectIntentResponse CreateDetectIntentResponse(Models.Intent? intent, string queryText, string sessionId)
    {
        var fulfillmentText = "Ответ не найден.";
        var textMessage = intent?.Responses.FirstOrDefault()?.Messages.FirstOrDefault(m => m.Type == "0");
        if (textMessage?.Speech?.FirstOrDefault() is { } speech)
        {
            fulfillmentText = speech;
        }

        var queryResult = new QueryResult
        {
            QueryText = queryText,
            FulfillmentText = fulfillmentText,
            Intent = new Google.Cloud.Dialogflow.V2.Intent
            {
                DisplayName = intent?.Name ?? "Default Fallback Intent",
                Name = $"{sessionId}/intents/{intent?.Id ?? Guid.NewGuid().ToString()}"
            },
            IntentDetectionConfidence = 0.85f,
            LanguageCode = "ru"
        };
        queryResult.FulfillmentMessages.Add(new Intent.Types.Message
        {
            Text = new Intent.Types.Message.Types.Text
            {
                Text_ = { fulfillmentText }
            }
        });

        return new DetectIntentResponse
        {
            ResponseId = Guid.NewGuid().ToString(),
            QueryResult = queryResult
        };
    }
}
