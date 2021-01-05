using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Google.Cloud.Dialogflow.V2;
using FillInTheTextBot.Services.Configuration;
using NLog;
using System.Linq;
using FillInTheTextBot.Models;
using GranSteL.DialogflowBalancer;
using GranSteL.Helpers.Redis.Extensions;

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

        private const int MaximumRequestLength = 30;

        private readonly Dictionary<string, string> _commandDictionary = new Dictionary<string, string>
        {
            {StartCommand, WelcomeEventName},
            {ErrorCommand, ErrorEventName}
        };

        private readonly Logger _log = LogManager.GetLogger(nameof(DialogflowService));

        private readonly DialogflowConfiguration _configuration;
        private readonly DialogflowClientsBalancer _balancer;
        private readonly IMapper _mapper;

        private readonly Dictionary<Source, Func<Request, EventInput>> _eventResolvers;

        public DialogflowService(
            DialogflowConfiguration configuration,
            DialogflowClientsBalancer balancer,
            IMapper mapper)
        {
            _configuration = configuration;
            _mapper = mapper;
            _balancer = balancer;

            _eventResolvers = new Dictionary<Source, Func<Request, EventInput>>
            {
                {Source.Yandex, YandexEventResolve},
            };
        }

        public async Task<Dialog> GetResponseAsync(string text, string sessionId, string requiredContext = null)
        {
            var request = new Request
            {
                Text = text,
                SessionId = sessionId,
            };

            return await GetResponseAsync(request);
        }

        public async Task<Dialog> GetResponseAsync(Request request)
        {
            var dialog = await _balancer.InvokeSessionsClient(request.SessionId,
                (sessionClient, context) => GetResponseInternalAsync(request, sessionClient, context));

            return dialog;
        }

        public Task DeleteAllContextsAsync(Request request)
        {
            return _balancer.InvokeContextsClient(request.SessionId,
                (client, context) => DeleteAllContextsInternalAsync(request.SessionId, context.ProjectId, client));
        }

        public Task SetContextAsync(string sessionId, string contextName, int lifeSpan = 1, IDictionary<string, string> parameters = null)
        {
            return _balancer.InvokeContextsClient(sessionId,
                (client, context) => SetContextInternalAsync(client, sessionId, context.ProjectId, contextName, lifeSpan, parameters));
        }

        private async Task<Dialog> GetResponseInternalAsync(Request request, SessionsClient client, DialogflowContext context)
        {
            var intentRequest = CreateQuery(request, context);

            if (_configuration.LogQuery)
                _log.Trace($"Request:{System.Environment.NewLine}{intentRequest.Serialize()}");

            DetectIntentResponse intentResponse = await client.DetectIntentAsync(intentRequest);

            if (_configuration.LogQuery)
                _log.Trace($"Response:{System.Environment.NewLine}{intentResponse.Serialize()}");

            var queryResult = intentResponse.QueryResult;

            var response = _mapper.Map<Dialog>(queryResult);

            return response;
        }

        private Task DeleteAllContextsInternalAsync(string sessionId, string projectId, ContextsClient client)
        {
            var session = CreateSession(projectId, sessionId);

            return client.DeleteAllContextsAsync(session);
        }

        private Task SetContextInternalAsync(ContextsClient client, string sessionId, string projectId, string contextName, int lifeSpan = 1, IDictionary<string, string> parameters = null)
        {
            var session = CreateSession(projectId, sessionId);

            return SetContextAsync(client, session, contextName, lifeSpan, parameters);
        }

        private DetectIntentRequest CreateQuery(Request request, DialogflowContext context)
        {
            var session = CreateSession(context.ProjectId, request.SessionId);

            var eventInput = ResolveEvent(request);

            var text = request.Text;

            if (text.Length > MaximumRequestLength)
            {
                text = request.Text.Substring(0, MaximumRequestLength);
            }

            var query = new QueryInput
            {
                Text = new TextInput
                {
                    Text = text,
                    LanguageCode = _configuration.LanguageCode
                }
            };

            if (eventInput != null)
            {
                query.Event = eventInput;
            }

            var intentRequest = new DetectIntentRequest
            {
                SessionAsSessionName = session,
                QueryInput = query
            };

            return intentRequest;
        }

        public EventInput ResolveEvent(Request request)
        {
            EventInput result;

            var sourceMessenger = request?.Source;

            if (sourceMessenger != null && _eventResolvers.ContainsKey(sourceMessenger.Value))
            {
                result = _eventResolvers[sourceMessenger.Value].Invoke(request);
            }
            else
            {
                result = EventByCommand(request?.Text);
            }

            return result;
        }

        private EventInput EventByCommand(string requestText)
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
                result = GetEvent(eventName);
            }

            return result;
        }

        private EventInput GetEvent(string name)
        {
            return new EventInput
            {
                Name = name,
                LanguageCode = _configuration.LanguageCode
            };
        }

        private EventInput YandexEventResolve(Request request)
        {
            EventInput result;

            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            if (string.IsNullOrEmpty(request.Text))
            {
                if (request.IsOldUser)
                {
                    result = GetEvent(EasyWelcomeEventName);
                }
                else
                {
                    result = GetEvent(WelcomeEventName);
                }
            }
            else
            {
                result = EventByCommand(request.Text);
            }

            return result;
        }

        private SessionName CreateSession(string projectId, string sessionsId)
        {
            var session = new SessionName(projectId, sessionsId);

            return session;
        }

        private Task SetContextAsync(ContextsClient client, SessionName sessionName, string contextName, int lifeSpan = 1, IDictionary<string, string> parameters = null)
        {
            var context = new Context
            {
                ContextName = new ContextName(_configuration.ProjectId, sessionName.SessionId, contextName),
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

            return client.CreateContextAsync(sessionName, context);
        }
    }
}
