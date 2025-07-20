using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.Extensions;
using FillInTheTextBot.Services.Rasa.Mapping;
using FillInTheTextBot.Services.Rasa.Models;
using GranSteL.Tools.ScopeSelector;
using Microsoft.Extensions.Logging;
using Environment = System.Environment;
using InternalModels = FillInTheTextBot.Models;

namespace FillInTheTextBot.Services;

public class RasaService : IDialogflowService
{
    private const string EventKey = "event:";
    private const string StartCommand = "/start";
    private const string ErrorCommand = "/error";

    private const string WelcomeEventName = "Welcome";
    private const string EasyWelcomeEventName = "EasyWelcome";
    private const string ErrorEventName = "Error";

    private const int MaximumRequestTextLength = 30;

    private readonly Dictionary<string, string> _commandDictionary = new()
    {
        { StartCommand, WelcomeEventName },
        { ErrorCommand, ErrorEventName }
    };

    private readonly Dictionary<InternalModels.Source, Func<InternalModels.Request, string>>
        _eventResolvers;

    private readonly ILogger<RasaService> _log;
    private readonly ScopesSelector<HttpClient> _httpClientSelector;

    public RasaService(
        ILogger<RasaService> log,
        ScopesSelector<HttpClient> httpClientSelector)
    {
        _log = log;
        _httpClientSelector = httpClientSelector;

        _eventResolvers = new Dictionary<InternalModels.Source, Func<InternalModels.Request, string>>
        {
            { InternalModels.Source.Yandex, DefaultWelcomeEventResolve },
            { InternalModels.Source.Sber, DefaultWelcomeEventResolve },
            { InternalModels.Source.Marusia, DefaultWelcomeEventResolve }
        };
    }

    public async Task<InternalModels.Dialog> GetResponseAsync(string text, string sessionId, string scopeKey)
    {
        var request = new InternalModels.Request
        {
            Text = text,
            SessionId = sessionId,
            ScopeKey = scopeKey
        };

        return await GetResponseAsync(request);
    }

    public async Task<InternalModels.Dialog> GetResponseAsync(InternalModels.Request request)
    {
        using (Tracing.Trace())
        {
            MemoryDiagnostics.LogMemoryUsage("RasaService.GetResponseAsync - Start");
            
            var dialog = await _httpClientSelector.Invoke((httpClient, context)
                => GetResponseInternalAsync(request, httpClient, context), request.ScopeKey);

            MemoryDiagnostics.LogMemoryUsage("RasaService.GetResponseAsync - End");
            return dialog;
        }
    }

    public Task SetContextAsync(string sessionId, string scopeKey, string contextName, int lifeSpan = 1,
        IDictionary<string, string> parameters = null)
    {
        using (Tracing.Trace())
        {
            return _httpClientSelector.Invoke((httpClient, context) =>
                SetContextInternalAsync(httpClient, sessionId, context, contextName, lifeSpan, parameters), scopeKey);
        }
    }

    private async Task<InternalModels.Dialog> GetResponseInternalAsync(InternalModels.Request request,
        HttpClient httpClient, ScopeContext context)
    {
        using (Tracing.Trace(s =>
               {
                   if (context != null) s.SetTag(nameof(context.ScopeId), context.ScopeId);
               }, "Get response from Rasa"))
        {
            MetricsCollector.Increment("rasa_webhook_scope", context.ScopeId);

            context.TryGetParameterValue(nameof(RasaConfiguration.BaseUrl), out var baseUrl);
            context.TryGetParameterValue(nameof(RasaConfiguration.LanguageCode), out var languageCode);
            context.TryGetParameterValue(nameof(RasaConfiguration.LogQuery), out var logQuery);

            var rasaRequest = CreateRasaRequest(request);

            bool.TryParse(logQuery, out var isLogQuery);

            if (isLogQuery)
                _log.LogTrace($"Rasa Request:{Environment.NewLine}{JsonSerializer.Serialize(rasaRequest)}");

            var requestContent = new StringContent(
                JsonSerializer.Serialize(rasaRequest), 
                Encoding.UTF8, 
                "application/json");

            var rasaUrl = $"{baseUrl ?? "http://localhost:5005"}/webhooks/rest/webhook";
            var httpResponse = await httpClient.PostAsync(rasaUrl, requestContent).ConfigureAwait(false);

            httpResponse.EnsureSuccessStatusCode();

            var responseContent = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            var rasaResponses = JsonSerializer.Deserialize<List<RasaResponse>>(responseContent);

            if (isLogQuery)
                _log.LogTrace($"Rasa Response:{Environment.NewLine}{responseContent}");

            MetricsCollector.Increment("intent", GetIntentFromResponse(rasaResponses));

            var response = rasaResponses.ToDialog();
            response.ScopeKey = context.ScopeId;

            return response;
        }
    }

    private async Task SetContextInternalAsync(HttpClient httpClient, string sessionId, ScopeContext scopeContext,
        string contextName, int lifeSpan = 1, IDictionary<string, string> parameters = null)
    {
        using (Tracing.Trace())
        {
            scopeContext.TryGetParameterValue(nameof(RasaConfiguration.BaseUrl), out var baseUrl);

            var contextRequest = new RasaContextRequest
            {
                SenderId = sessionId,
                Slots = new Dictionary<string, object>()
            };

            if (parameters?.Count > 0)
            {
                foreach (var param in parameters)
                {
                    contextRequest.Slots.Add(param.Key, param.Value);
                }
            }

            // Устанавливаем контекст как слот
            contextRequest.Slots.Add(contextName, true);

            MetricsCollector.Increment("rasa_context_scope", scopeContext.ScopeId);

            var requestContent = new StringContent(
                JsonSerializer.Serialize(contextRequest), 
                Encoding.UTF8, 
                "application/json");

            var rasaUrl = $"{baseUrl ?? "http://localhost:5005"}/conversations/{sessionId}/tracker/slots";
            await httpClient.PutAsync(rasaUrl, requestContent).ConfigureAwait(false);
        }
    }

    private RasaRequest CreateRasaRequest(InternalModels.Request request)
    {
        using (Tracing.Trace())
        {
            var eventText = ResolveEvent(request);
            var text = request.Text;

            if (text?.Length > MaximumRequestTextLength) 
                text = request.Text.Substring(0, MaximumRequestTextLength);

            // Если есть event, отправляем его как префикс
            var messageText = !string.IsNullOrEmpty(eventText) ? $"{EventKey}{eventText}" : text ?? string.Empty;

            return new RasaRequest
            {
                Sender = request.SessionId,
                Message = messageText
            };
        }
    }

    private string ResolveEvent(InternalModels.Request request)
    {
        using (Tracing.Trace())
        {
            string result;

            var sourceMessenger = request?.Source;

            if (sourceMessenger != null && _eventResolvers.ContainsKey(sourceMessenger.Value))
                result = _eventResolvers[sourceMessenger.Value].Invoke(request);
            else
                result = EventByCommand(request?.Text);

            return result;
        }
    }

    private string EventByCommand(string requestText)
    {
        using (Tracing.Trace())
        {
            _commandDictionary.TryGetValue(requestText, out var eventName);

            var splitted = requestText?.Split(new[] { EventKey }, StringSplitOptions.None);

            if (splitted?.Length == 2) eventName = splitted.LastOrDefault();

            return eventName;
        }
    }

    private string DefaultWelcomeEventResolve(InternalModels.Request request)
    {
        using (Tracing.Trace())
        {
            string result;

            if (string.IsNullOrEmpty(request.Text))
            {
                if (request.IsOldUser)
                    result = EasyWelcomeEventName;
                else
                    result = WelcomeEventName;
            }
            else
            {
                result = EventByCommand(request.Text);
            }

            return result;
        }
    }

    private static string GetIntentFromResponse(List<RasaResponse> responses)
    {
        var firstResponse = responses?.FirstOrDefault();
        if (firstResponse?.Custom?.ContainsKey("intent") == true)
        {
            return firstResponse.Custom["intent"]?.ToString() ?? "unknown";
        }
        return "unknown";
    }
}