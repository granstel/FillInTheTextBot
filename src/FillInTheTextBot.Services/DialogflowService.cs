using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Google.Cloud.Dialogflow.V2;
using NLog;
using System.Linq;
using GranSteL.Helpers.Redis.Extensions;
using GranSteL.Tools.ScopeSelector;
using OpenTracing.Util;
using InternalModels = FillInTheTextBot.Models;

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

        private readonly Dictionary<string, string> _commandDictionary = new Dictionary<string, string>
        {
            {StartCommand, WelcomeEventName},
            {ErrorCommand, ErrorEventName}
        };

        private readonly Logger _log = LogManager.GetLogger(nameof(DialogflowService));

        private readonly ScopesSelector<SessionsClient> _sessionsClientBalancer;
        private readonly ScopesSelector<ContextsClient> _contextsClientBalancer;
        private readonly IMapper _mapper;

        private readonly Dictionary<InternalModels.Source, Func<InternalModels.Request, string, EventInput>> _eventResolvers;

        public DialogflowService(
            IMapper mapper,
            ScopesSelector<SessionsClient> sessionsClientBalancer,
            ScopesSelector<ContextsClient> contextsClientBalancer)
        {
            _mapper = mapper;
            _sessionsClientBalancer = sessionsClientBalancer;
            _contextsClientBalancer = contextsClientBalancer;

            _eventResolvers = new Dictionary<InternalModels.Source, Func<InternalModels.Request, string, EventInput>>
            {
                {InternalModels.Source.Yandex, DefaultWelcomeEventResolve},
                {InternalModels.Source.Sber, DefaultWelcomeEventResolve},
                {InternalModels.Source.Marusia, DefaultWelcomeEventResolve}
            };
        }

        public async Task<InternalModels.Dialog> GetResponseAsync(string text, string sessionId, string requiredContext = null)
        {
            var request = new InternalModels.Request
            {
                Text = text,
                SessionId = sessionId,
            };

            return await GetResponseAsync(request);
        }

        public async Task<InternalModels.Dialog> GetResponseAsync(InternalModels.Request request)
        {
            var dialog = await _sessionsClientBalancer.Invoke(request.SessionId,
                (sessionClient, context) => GetResponseInternalAsync(request, sessionClient, context), request.ScopeKey);

            return dialog;
        }

        public Task DeleteAllContextsAsync(InternalModels.Request request)
        {
            return _contextsClientBalancer.Invoke(request.SessionId,
                (client, context) => DeleteAllContextsInternalAsync(request.SessionId, context.Parameters["ProjectId"], client),
                    request.ScopeKey);
        }

        public Task SetContextAsync(string sessionId, string contextName, int lifeSpan = 1, IDictionary<string, string> parameters = null)
        {
            return _contextsClientBalancer.Invoke(sessionId,
                (client, context) => SetContextInternalAsync(client, sessionId, context.Parameters["ProjectId"], contextName, lifeSpan, parameters));
        }

        private async Task<InternalModels.Dialog> GetResponseInternalAsync(InternalModels.Request request, SessionsClient client, ScopeContext context)
        {
            using (Tracing.Trace(s => s.WithTag(nameof(context.ScopeId), context.ScopeId)))
            {
                var intentRequest = CreateQuery(request, context);

                bool.TryParse(context.Parameters["LogQuery"], out var isLogQuery);

                if (isLogQuery)
                    _log.Trace($"Request:{System.Environment.NewLine}{intentRequest.Serialize()}");

                DetectIntentResponse intentResponse = await client.DetectIntentAsync(intentRequest);

                if (isLogQuery)
                    _log.Trace($"Response:{System.Environment.NewLine}{intentResponse.Serialize()}");

                var queryResult = intentResponse.QueryResult;

                var response = _mapper.Map<InternalModels.Dialog>(queryResult);

                response.ScopeKey = context.Parameters["ProjectId"];

                return response;
            }
        }

        private Task DeleteAllContextsInternalAsync(string sessionId, string projectId, ContextsClient client)
        {
            var session = CreateSession(projectId, sessionId);

            return client.DeleteAllContextsAsync(session);
        }

        private Task SetContextInternalAsync(ContextsClient client, string sessionId, string projectId, string contextName, int lifeSpan = 1, IDictionary<string, string> parameters = null)
        {
            var session = CreateSession(projectId, sessionId);

            var context = GetContext(projectId, session, contextName, lifeSpan, parameters);

            return client.CreateContextAsync(session, context);
        }

        private DetectIntentRequest CreateQuery(InternalModels.Request request, ScopeContext context)
        {
            var session = CreateSession(context.Parameters["ProjectId"], request.SessionId);

            var languageCode = context.Parameters["LanguageCode"];

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
                QueryParams = new QueryParameters()
            };

            var contexts = request.RequiredContexts.Select(c =>
                GetContext(context.Parameters["ProjectId"], session, c.Name, c.LifeSpan, c.Parameters)).ToList();

            intentRequest.QueryParams.Contexts.AddRange(contexts);

            return intentRequest;
        }

        private EventInput ResolveEvent(InternalModels.Request request, string languageCode)
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

        private EventInput EventByCommand(string requestText, string languageCode)
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

        private SessionName CreateSession(string projectId, string sessionsId)
        {
            var session = new SessionName(projectId, sessionsId);

            return session;
        }

        private Context GetContext(string projectId, SessionName sessionName, string contextName, int lifeSpan = 1, IDictionary<string, string> parameters = null)
        {
            var context = new Context
            {
                ContextName = new ContextName(projectId, sessionName.SessionId, contextName),
                LifespanCount = lifeSpan,
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
