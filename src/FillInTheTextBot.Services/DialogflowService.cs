using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Google.Cloud.Dialogflow.V2;
using FillInTheTextBot.Models.Internal;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.Extensions;
using NLog;
using System.Linq;

namespace FillInTheTextBot.Services
{
    public class DialogflowService : IDialogflowService
    {
        private const string EventKey = "event:";
        private const string StartCommand = "/start";
        private const string ErrorCommand = "/error";

        private const string WelcomeEventName = "Welcome";
        private const string ErrorEventName = "Error";

        private readonly Dictionary<string, string> _commandDictionary = new Dictionary<string, string>
        {
            {StartCommand, WelcomeEventName},
            {ErrorCommand, ErrorEventName}
        };

        private readonly Logger _log = LogManager.GetLogger(nameof(DialogflowService));

        private readonly SessionsClient _sessionsClient;
        private readonly ContextsClient _contextsClient;
        private readonly DialogflowConfiguration _configuration;
        private readonly IMapper _mapper;

        private readonly Dictionary<Source, Func<Request, EventInput>> _eventResolvers;

        public DialogflowService(SessionsClient sessionsClient, DialogflowConfiguration configuration, IMapper mapper, ContextsClient contextsClient)
        {
            _sessionsClient = sessionsClient;
            _configuration = configuration;
            _mapper = mapper;

            _eventResolvers = new Dictionary<Source, Func<Request, EventInput>>
            {
                {Source.Yandex, YandexEventResolve},
            };
            _contextsClient = contextsClient;
        }

        public async Task<Dialog> GetResponseAsync(string text, string sessionId)
        {
            var request = new Request
            {
                Text = text,
                SessionId = sessionId
            };

            return await GetResponseAsync(request);
        }

        public async Task<Dialog> GetResponseAsync(Request request)
        {
            var intentRequest = CreateQuery(request);

            var contextTestTwo = _contextsClient.CreateContext(intentRequest.SessionAsSessionName, new Context 
            {
                ContextName = new ContextName(_configuration.ProjectId, request.SessionId, "test-2"),
                LifespanCount = 1
            });

            var contexts = _contextsClient.ListContexts(intentRequest.SessionAsSessionName).ToList();


            if (_configuration.LogQuery)
                _log.Trace($"Request:{System.Environment.NewLine}{intentRequest.Serialize()}");

            var intentResponse = await _sessionsClient.DetectIntentAsync(intentRequest);

            if (_configuration.LogQuery)
                _log.Trace($"Response:{System.Environment.NewLine}{intentResponse.Serialize()}");

            var queryResult = intentResponse.QueryResult;

            var response = _mapper.Map<Dialog>(queryResult);


            contexts = _contextsClient.ListContexts(intentRequest.SessionAsSessionName).ToList();
            
            return response;
        }

        private DetectIntentRequest CreateQuery(Request request)
        {
            var session = new SessionName(_configuration.ProjectId, request.SessionId);

            var eventInput = ResolveEvent(request);

            var query = new QueryInput
            {
                Text = new TextInput
                {
                    Text = request.Text,
                    LanguageCode = _configuration.LanguageCode
                },
            };

            if (eventInput != null)
            {
                query.Event = eventInput;
            }

            var intentRequest = new DetectIntentRequest
            {
                SessionAsSessionName = session,
                QueryInput = query,
            };

            return intentRequest;
        }

        public EventInput ResolveEvent(Request request)
        {
            var result = default(EventInput);

            var sourceMessenger = request?.Source;

            if (sourceMessenger != null && _eventResolvers.ContainsKey(sourceMessenger.Value))
            {
                result = _eventResolvers[sourceMessenger.Value].Invoke(request);
            }
            else
            {
                result = EventByCommand(request.Text);
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
            if (request.NewSession == true && string.IsNullOrEmpty(request.Text))
            {
                result = GetEvent(WelcomeEventName);
            }
            else
            {
                result = EventByCommand(request.Text);
            }

            return result;
        }
    }
}
