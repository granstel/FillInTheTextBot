using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Dialogflow.V2;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Services;

/// <summary>
/// HTTP клиент для эмулятора Dialogflow, который реализует интерфейс SessionsClient
/// </summary>
public class DialogflowEmulatorClient : SessionsClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<DialogflowEmulatorClient> _logger;

    public DialogflowEmulatorClient(HttpClient httpClient, string baseUrl, ILogger<DialogflowEmulatorClient> logger = null)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _logger = logger;
    }

    public override async Task<DetectIntentResponse> DetectIntentAsync(DetectIntentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionName = request.SessionAsSessionName;
            var projectId = sessionName.ProjectId;
            var sessionId = sessionName.SessionId;
            
            // Создаем HTTP запрос в формате нашего эмулятора
            var emulatorRequest = new
            {
                queryInput = ConvertQueryInput(request.QueryInput),
                queryParams = request.QueryParams != null ? new
                {
                    resetContexts = request.QueryParams.ResetContexts,
                    contexts = request.QueryParams.Contexts?.Select(ConvertContext).ToList()
                } : null
            };

            var json = JsonSerializer.Serialize(emulatorRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"{_baseUrl}/v2/projects/{projectId}/agent/sessions/{sessionId}:detectIntent";

            _logger?.LogTrace($"Sending request to emulator: {url}");
            _logger?.LogTrace($"Request body: {json}");

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Emulator request failed: {response.StatusCode}, {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            _logger?.LogTrace($"Response: {responseJson}");

            var emulatorResponse = JsonSerializer.Deserialize<EmulatorDetectIntentResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return ConvertToDetectIntentResponse(emulatorResponse);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error calling Dialogflow emulator");
            throw;
        }
    }

    private object ConvertQueryInput(QueryInput queryInput)
    {
        if (queryInput.Text != null)
        {
            return new
            {
                text = new
                {
                    text = queryInput.Text.Text,
                    languageCode = queryInput.Text.LanguageCode
                }
            };
        }

        if (queryInput.Event != null)
        {
            return new
            {
                @event = new
                {
                    name = queryInput.Event.Name,
                    languageCode = queryInput.Event.LanguageCode,
                    parameters = queryInput.Event.Parameters?.Fields?.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.StringValue ?? kvp.Value?.ToString()
                    )
                }
            };
        }

        return new { };
    }

    private object ConvertContext(Context context)
    {
        return new
        {
            name = context.ContextName?.ToString(),
            lifespanCount = context.LifespanCount,
            parameters = context.Parameters?.Fields?.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.StringValue ?? kvp.Value?.ToString()
            )
        };
    }

    private DetectIntentResponse ConvertToDetectIntentResponse(EmulatorDetectIntentResponse emulatorResponse)
    {
        var response = new DetectIntentResponse
        {
            ResponseId = emulatorResponse.ResponseId,
            QueryResult = new QueryResult
            {
                QueryText = emulatorResponse.QueryResult.QueryText,
                LanguageCode = emulatorResponse.QueryResult.LanguageCode,
                FulfillmentText = emulatorResponse.QueryResult.FulfillmentText,
                IntentDetectionConfidence = emulatorResponse.QueryResult.IntentDetectionConfidence,
                Parameters = new Struct(),
                AllRequiredParamsPresent = emulatorResponse.QueryResult.AllRequiredParamsPresent
            }
        };

        if (emulatorResponse.QueryResult.Intent != null)
        {
            response.QueryResult.Intent = new Intent
            {
                IntentName = IntentName.FromProjectIntent(
                    ExtractProjectId(emulatorResponse.QueryResult.Intent.Name),
                    ExtractIntentId(emulatorResponse.QueryResult.Intent.Name)
                ),
                DisplayName = emulatorResponse.QueryResult.Intent.DisplayName
            };
        }

        if (emulatorResponse.QueryResult.FulfillmentMessages != null)
        {
            foreach (var message in emulatorResponse.QueryResult.FulfillmentMessages)
            {
                if (message.Text?.Text != null && message.Text.Text.Count > 0)
                {
                    response.QueryResult.FulfillmentMessages.Add(new Intent.Types.Message
                    {
                        Text = new Intent.Types.Message.Types.Text
                        {
                            Text_ = { message.Text.Text }
                        }
                    });
                }
            }
        }

        return response;
    }

    private string ExtractProjectId(string intentName)
    {
        // projects/PROJECT_ID/agent/intents/INTENT_ID
        var parts = intentName?.Split('/');
        return parts?.Length >= 2 ? parts[1] : "unknown";
    }

    private string ExtractIntentId(string intentName)
    {
        // projects/PROJECT_ID/agent/intents/INTENT_ID
        var parts = intentName?.Split('/');
        return parts?.Length >= 4 ? parts[3] : "unknown";
    }

    // Заглушки для других методов базового класса
    public override Task<DetectIntentResponse> DetectIntentAsync(string session, QueryInput queryInput, CancellationToken cancellationToken = default)
    {
        var sessionName = SessionName.Parse(session);
        var request = new DetectIntentRequest
        {
            SessionAsSessionName = sessionName,
            QueryInput = queryInput
        };
        return DetectIntentAsync(request, cancellationToken);
    }

    // HttpClient will be disposed by GC since we're not implementing IDisposable pattern
    // in the base class hierarchy
}

// Классы для десериализации ответа эмулятора
public class EmulatorDetectIntentResponse
{
    public string ResponseId { get; set; }
    public EmulatorQueryResult QueryResult { get; set; }
}

public class EmulatorQueryResult
{
    public string QueryText { get; set; }
    public string LanguageCode { get; set; }
    public string FulfillmentText { get; set; }
    public float IntentDetectionConfidence { get; set; }
    public Dictionary<string, object> Parameters { get; set; }
    public bool AllRequiredParamsPresent { get; set; }
    public EmulatorIntent Intent { get; set; }
    public List<EmulatorFulfillmentMessage> FulfillmentMessages { get; set; }
}

public class EmulatorIntent
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
}

public class EmulatorFulfillmentMessage
{
    public EmulatorTextMessage Text { get; set; }
}

public class EmulatorTextMessage
{
    public List<string> Text { get; set; }
}