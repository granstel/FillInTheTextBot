using System;
using System.Threading.Tasks;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Extensions;
using GranSteL.Helpers.Redis;
using MailRu.Marusia.Models;
using MailRu.Marusia.Models.Input;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Messengers.Marusia
{
    public class MarusiaService : MessengerService<InputModel, OutputModel>, IMarusiaService
    {
        private const string PingCommand = "ping";
        private const string PongResponse = "pong";

        private readonly IRedisCacheService _cache;

        public MarusiaService(
            ILogger<MarusiaService> log,
            IConversationService conversationService,
            IRedisCacheService cache) : base(log, conversationService)
        {
            _cache = cache;
        }

        protected override Models.Request Before(InputModel input)
        {
            var request = input.ToRequest();

            input.TryGetFromUserState(Models.Request.IsOldUserKey, out bool isOldUser);

            request.IsOldUser = isOldUser;

            input.TryGetFromUserState(Models.Response.NextTextIndexStorageKey, out object nextTextIndex);

            request.NextTextIndex = Convert.ToInt32(nextTextIndex);

            input.TryGetFromSessionState(Models.Response.ScopeStorageKey, out string scopeKey);

            request.ScopeKey = scopeKey;

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

            output.AddToSessionState(Models.Response.ScopeStorageKey, response.ScopeKey);

            _cache.AddAsync($"marusia:{input.Session?.UserId}", string.Empty, TimeSpan.FromDays(14)).Forget();

            return Task.FromResult(output);
        }
    }
}
