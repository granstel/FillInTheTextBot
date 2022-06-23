using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using AutoMapper;
using FillInTheTextBot.Services;
using Microsoft.Extensions.Logging;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Input;

namespace FillInTheTextBot.Messengers.Yandex
{
    public class YandexService : MessengerService<InputModel, OutputModel>, IYandexService
    {
        private const string PingCommand = "ping";
        private const string PongResponse = "pong";

        private readonly IMapper _mapper;
        private readonly Stopwatch _stopwatch;

        public YandexService(
            ILogger<YandexService> log,
            IConversationService conversationService,
            IMapper mapper) : base(log, conversationService, mapper)
        {
            _mapper = mapper;
            _stopwatch = new Stopwatch();
        }

        protected override Models.Request Before(InputModel input)
        {
            _stopwatch.Start();
            var request = base.Before(input);

            input.TryGetFromUserState(Models.Request.IsOldUserKey, out bool isOldUser);

            request.IsOldUser = isOldUser;

            if (input.TryGetFromUserState(Models.Response.NextTextIndexStorageKey, out object nextTextIndex) != true)
            {
                input.TryGetFromApplicationState(Models.Response.NextTextIndexStorageKey, out nextTextIndex);
            }

            request.NextTextIndex = Convert.ToInt32(nextTextIndex);

            input.TryGetFromSessionState(Models.Response.ScopeStorageKey, out string scopeKey);

            request.ScopeKey = scopeKey;

            if (request.NewSession == true)
            {
                var contexts = GetContexts(input);

                request.RequiredContexts.AddRange(contexts);
            }

            return request;
        }

        protected override Models.Response ProcessCommand(Models.Request request)
        {
            Models.Response response = null;

            if (PingCommand.Equals(request.Text, StringComparison.InvariantCultureIgnoreCase))
            {
                response = new Models.Response { Text = PongResponse };
            }

            return response;
        }

        protected override async Task<OutputModel> AfterAsync(InputModel input, Models.Response response)
        {
            var output = await base.AfterAsync(input, response);

            _mapper.Map(input, output);

            output.AddToUserState(Models.Request.IsOldUserKey, true);

            output.AddToUserState(Models.Response.NextTextIndexStorageKey, response.NextTextIndex);
            output.AddToApplicationState(Models.Response.NextTextIndexStorageKey, response.NextTextIndex);

            output.AddToSessionState(Models.Response.ScopeStorageKey, response.ScopeKey);

            _stopwatch.Stop();

            if (_stopwatch.ElapsedMilliseconds > 1000)
            {
                output.AddAnalyticsEvent("ElapsedTime", new Dictionary<string, object> { { "ElapsedMilliseconds", _stopwatch.ElapsedMilliseconds } });
            }

            output.AddAnalyticsEvent(Models.Response.ScopeStorageKey, new Dictionary<string, object> { { Models.Response.ScopeStorageKey, response.ScopeKey } });

            return output;
        }

        private ICollection<Models.Context> GetContexts(InputModel input)
        {
            var contexts = new List<Models.Context>();

            if (input.IsNavigator())
            {
                contexts.Add(new Models.Context
                {
                    Name = "navigator",
                    LifeSpan = 50000
                });
            }

            if (input.IsCanShowAdvertising())
            {
                contexts.Add(new Models.Context
                {
                    Name = "advertising",
                    LifeSpan = 50000
                });
            }

            return contexts;
        }
    }
}
