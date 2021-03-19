using System;
using System.Threading.Tasks;
using AutoMapper;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services;
using GranSteL.Helpers.Redis;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Input;

namespace FillInTheTextBot.Messengers.Marusia
{
    public class MarusiaService : MessengerService<InputModel, OutputModel>, IMarusiaService
    {
        private const string PingCommand = "ping";
        private const string PongResponse = "pong";

        private readonly IMapper _mapper;
        private readonly IRedisCacheService _cache;

        public MarusiaService(
            IConversationService conversationService,
            IMapper mapper,
            IDialogflowService dialogflowService,
            IRedisCacheService cache) : base(conversationService, mapper, dialogflowService)
        {
            _mapper = mapper;
            _cache = cache;
        }

        protected override Models.Request Before(InputModel input)
        {
            var request = base.Before(input);

            var userStateCacheKey = GetCacheKey(request.UserHash);

            _cache.TryGet(userStateCacheKey, out UserState userState);

            request.IsOldUser = userState?.IsOldUser ?? false;

            request.NextTextIndex = Convert.ToInt32(userState?.NextTextIndex ?? 0);

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

            var userState = new Models.UserState
            {
                IsOldUser = true,
                NextTextIndex = response.NextTextIndex
            };

            var userStateCacheKey = GetCacheKey(response.UserHash);

            await _cache.TryAddAsync(userStateCacheKey, userState, TimeSpan.FromDays(14));

            return output;
        }

        private string GetCacheKey(string key)
        {
            return $"marusia:{key}";
        }
    }
}
