using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Extensions;
using Microsoft.Extensions.Logging;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Input;

namespace FillInTheTextBot.Messengers.Yandex
{
    public class YandexService : MessengerService<InputModel, OutputModel>, IYandexService
    {
        private const string PingCommand = "ping";
        private const string PongResponse = "pong";

        private readonly Stopwatch _stopwatch;

        public YandexService(
            ILogger<YandexService> log,
            IConversationService conversationService) : base(log, conversationService)
        {
            _stopwatch = new Stopwatch();
        }

        protected override Models.Request Before(InputModel input)
        {
            _stopwatch.Start();

            var request = input.ToRequest();

            input.TryGetFromUserState(Models.Request.IsOldUserKey, out bool isOldUser);
            request.IsOldUser = isOldUser;

            input.TryGetFromUserState(Models.Response.NextTextIndexStorageKey, out long nextTextIndex);
            request.NextTextIndex = Convert.ToInt32(nextTextIndex);

            input.TryGetFromSessionState(Models.Response.ScopeStorageKey, out string scopeKey);
            request.ScopeKey = scopeKey;

            input.TryGetFromUserState(Models.Response.PassedTextsKey, out object passedTexts);
            request.PassedTexts = passedTexts.ToString().Deserialize<string[]>();

            if (request.NewSession != true) return request;

            var contexts = GetContexts(input);
            request.RequiredContexts.AddRange(contexts);

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

        protected override Task<OutputModel> AfterAsync(InputModel input, Models.Response response)
        {
            var output = response.ToOutput();

            output = input.FillOutput(output);

            output.AddToUserState(Models.Request.IsOldUserKey, true);

            output.AddToUserState(Models.Response.NextTextIndexStorageKey, response.NextTextIndex);

            output.AddToUserState(Models.Response.PassedTextsKey, response.PassedTexts);

            output.AddToSessionState(Models.Response.ScopeStorageKey, response.ScopeKey);

            _stopwatch.Stop();

            if (_stopwatch.ElapsedMilliseconds > 1000)
            {
                output.AddAnalyticsEvent("ElapsedTime", new Dictionary<string, object> { { "ElapsedMilliseconds", _stopwatch.ElapsedMilliseconds } });
            }

            output.AddAnalyticsEvent(Models.Response.ScopeStorageKey, new Dictionary<string, object> { { Models.Response.ScopeStorageKey, response.ScopeKey } });

            return Task.FromResult(output);
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
