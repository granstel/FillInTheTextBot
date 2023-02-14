using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Cloud.Dialogflow.V2;
using System.Linq;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.Extensions;
using GranSteL.Tools.ScopeSelector;
using InternalModels = FillInTheTextBot.Models;
using Microsoft.Extensions.Logging;
using FillInTheTextBot.Services.Mapping;
using Prometheus;

namespace FillInTheTextBot.Services
{
    public class DialogflowService : IDialogflowService
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
            {StartCommand, WelcomeEventName},
            {ErrorCommand, ErrorEventName}
        };

        private readonly ILogger<DialogflowService> _log;

        private readonly ScopesSelector<SessionsClient> _sessionsClientSelector;
        private readonly ScopesSelector<ContextsClient> _contextsClientSelector;

        private readonly Dictionary<InternalModels.Source, Func<InternalModels.Request, string, EventInput>> _eventResolvers;

        public DialogflowService(
            ILogger<DialogflowService> log,
            ScopesSelector<SessionsClient> sessionsClientSelector,
            ScopesSelector<ContextsClient> contextsClientSelector)
        {
            _log = log;
            _sessionsClientSelector = sessionsClientSelector;
            _contextsClientSelector = contextsClientSelector;

            _eventResolvers = new Dictionary<InternalModels.Source, Func<InternalModels.Request, string, EventInput>>
            {
                {InternalModels.Source.Yandex, DefaultWelcomeEventResolve},
                {InternalModels.Source.Sber, DefaultWelcomeEventResolve},
                {InternalModels.Source.Marusia, DefaultWelcomeEventResolve}
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
                var dialog = await _sessionsClientSelector.Invoke((sessionClient, context) 
                    => GetResponseInternalAsync(request, sessionClient, context), request.ScopeKey);

                return dialog;
            }
        }

        public Task SetContextAsync(string sessionId, string scopeKey, string contextName, int lifeSpan = 1,
            IDictionary<string, string> parameters = null)
        {
            using (Tracing.Trace())
            {
                return _contextsClientSelector.Invoke((client, context) => 
                    SetContextInternalAsync(client, sessionId, context, contextName, lifeSpan, parameters), scopeKey);
            }
        }

        private async Task<InternalModels.Dialog> GetResponseInternalAsync(InternalModels.Request request, SessionsClient client, ScopeContext context)
        {
            using (Tracing.Trace(s => s.WithTag(nameof(context.ScopeId), context.ScopeId), "Get response from Dialogflow"))
            {
                MetricsCollector.Increment("dialogflow_DetectIntent_scope", context.ScopeId);

                context.TryGetParameterValue(nameof(DialogflowConfiguration.ProjectId), out string projectId);
                context.TryGetParameterValue(nameof(DialogflowConfiguration.LanguageCode), out string languageCode);
                context.TryGetParameterValue(nameof(DialogflowConfiguration.Region), out string region);
                context.TryGetParameterValue(nameof(DialogflowConfiguration.LogQuery), out string logQuery);

                var intentRequest = CreateQuery(request, projectId, languageCode, region);

                bool.TryParse(logQuery, out var isLogQuery);

                if (isLogQuery)
                    _log.LogTrace($"Request:{System.Environment.NewLine}{intentRequest.Serialize()}");

                DetectIntentResponse intentResponse = await client.DetectIntentAsync(intentRequest).ConfigureAwait(false);

                if (isLogQuery)
                    _log.LogTrace($"Response:{System.Environment.NewLine}{intentResponse.Serialize()}");

                var queryResult = intentResponse.QueryResult;

                MetricsCollector.Increment("intent", queryResult.Intent.DisplayName);

                var response = queryResult.ToDialog();

                response.ScopeKey = context.ScopeId;

                return response;
            }
        }

        private Task SetContextInternalAsync(ContextsClient client, string sessionId, ScopeContext scopeContext, string contextName, int lifeSpan = 1, IDictionary<string, string> parameters = null)
        {
            using (Tracing.Trace())
            {
                scopeContext.TryGetParameterValue(nameof(DialogflowConfiguration.ProjectId), out string projectId);
                scopeContext.TryGetParameterValue(nameof(DialogflowConfiguration.Region), out string region);

                var session = CreateSession(projectId, region, sessionId);

                var context = GetContext(projectId, region, session, contextName, lifeSpan, parameters);

                MetricsCollector.Increment("dialogflow_CreateContext_scope", scopeContext.ScopeId);

                return client.CreateContextAsync(session, context);
            }
        }

        private DetectIntentRequest CreateQuery(InternalModels.Request request, string projectId, string languageCode,
            string region)
        {
            using (Tracing.Trace())
            {
                var session = CreateSession(projectId, region, request.SessionId);

                var eventInput = ResolveEvent(request, languageCode);

                var text = request.Text;

                if (text?.Length > MaximumRequestTextLength)
                {
                    text = request.Text.Substring(0, MaximumRequestTextLength);
                }

                var query = new QueryInput
                {
                    Text = new TextInput
                    {
                        Text = text ?? string.Empty,
                        LanguageCode = languageCode
                    }
                };

                if (eventInput != null)
                {
                    query.Event = eventInput;
                }

                var intentRequest = new DetectIntentRequest
                {
                    SessionAsSessionName = session,
                    QueryInput = query,
                    QueryParams = new QueryParameters
                    {
                        ResetContexts = request.ResetContexts
                    }
                };

                var contexts = request.RequiredContexts.Select(c =>
                    GetContext(projectId, region, session, c.Name, c.LifeSpan, c.Parameters)).ToList();

                intentRequest.QueryParams.Contexts.AddRange(contexts);

                return intentRequest;
            }
        }

        private EventInput ResolveEvent(InternalModels.Request request, string languageCode)
        {
            using (Tracing.Trace())
            {
                EventInput result;

                var sourceMessenger = request?.Source;

                if (sourceMessenger != null && _eventResolvers.ContainsKey(sourceMessenger.Value))
                {
                    result = _eventResolvers[sourceMessenger.Value].Invoke(request, languageCode);
                }
                else
                {
                    result = EventByCommand(request?.Text, languageCode);
                }

                return result;
            }
        }

        private EventInput EventByCommand(string requestText, string languageCode)
        {
            using (Tracing.Trace())
            {
                var result = default(EventInput);

                _commandDictionary.TryGetValue(requestText, out var eventName);

                var splitted = requestText.Split(new[] { EventKey }, StringSplitOptions.None);

                if (splitted.Length == 2)
                {
                    eventName = splitted.LastOrDefault();
                }

                if (!string.IsNullOrEmpty(eventName))
                {
                    result = GetEvent(eventName, languageCode);
                }

                return result;
            }
        }

        private EventInput GetEvent(string name, string languageCode)
        {
            return new EventInput
            {
                Name = name,
                LanguageCode = languageCode
            };
        }

        private EventInput DefaultWelcomeEventResolve(InternalModels.Request request, string languageCode)
        {
            using (Tracing.Trace())
            {
                EventInput result;

                // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                if (string.IsNullOrEmpty(request.Text))
                {
                    if (request.IsOldUser)
                    {
                        result = GetEvent(EasyWelcomeEventName, languageCode);
                    }
                    else
                    {
                        result = GetEvent(WelcomeEventName, languageCode);
                    }
                }
                else
                {
                    result = EventByCommand(request.Text, languageCode);
                }

                return result;
            }
        }

        private SessionName CreateSession(string projectId, string locationId, string sessionId)
        {
            if (string.IsNullOrWhiteSpace(locationId))
            {
                return SessionName.FromProjectSession(projectId, sessionId);
            }

            return SessionName.FromProjectLocationSession(projectId, locationId, sessionId);
        }

        private ContextName CreateContext(string projectId, string locationId, string sessionId, string contextId)
        {
            if (string.IsNullOrWhiteSpace(locationId))
            {
                return ContextName.FromProjectSessionContext(projectId, sessionId, contextId);
            }

            return ContextName.FromProjectLocationSessionContext(projectId, locationId, sessionId, contextId);
        }

        private Context GetContext(string projectId, string region, SessionName sessionName, string contextId,
            int lifeSpan = 1, IDictionary<string, string> parameters = null)
        {
            using (Tracing.Trace())
            {
                var contextName = CreateContext(projectId, region, sessionName.SessionId, contextId);
                
                var context = new Context
                {
                    ContextName = contextName,
                    LifespanCount = lifeSpan
                };

                if (parameters?.Any() == true)
                {
                    context.Parameters = new Google.Protobuf.WellKnownTypes.Struct();

                    foreach (var parameter in parameters)
                    {
                        var value = new Google.Protobuf.WellKnownTypes.Value
                        {
                            StringValue = parameter.Value
                        };

                        context.Parameters.Fields.Add(parameter.Key, value);
                    }
                }

                return context;
            }
        }
    }
}
