using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services;
using GranSteL.Helpers.Redis;
using NLog;
using Request = Sber.SmartApp.Models.Request;
using Response = Sber.SmartApp.Models.Response;

namespace FillInTheTextBot.Messengers.Sber
{
    public class SberService : MessengerService<Request, Response>, ISberService
    {
        private const string ErrorAnswer = "Прости, у меня какие-то проблемы... Давай попробуем ещё раз. Если повторится, найди в ВК паблик \"Занимательные истории Алисы из Яндекса\" и напиши об этом в личку";

        private readonly IMapper _mapper;
        private readonly IRedisCacheService _cache;

        private readonly Logger _log = LogManager.GetLogger(nameof(SberService));

        public SberService(
            IConversationService conversationService,
            IMapper mapper,
            IDialogflowService dialogflowService, IRedisCacheService cache) : base(conversationService, mapper, dialogflowService)
        {
            _mapper = mapper;
            _cache = cache;
        }

        protected override Models.Request Before(Request input)
        {
            var request = base.Before(input);

            var userStateCacheKey = GetCacheKey(request.UserHash);

            _cache.TryGet(userStateCacheKey, out UserState userState);

            request.IsOldUser = userState?.IsOldUser ?? false;

            request.NextTextIndex = Convert.ToInt32(userState?.NextTextIndex ?? 0);

            var contexts = GetContexts(input);

            request.RequiredContexts.AddRange(contexts);

            return request;
        }

        protected override async Task<Response> AfterAsync(Request input, Models.Response response)
        {
            var output = await base.AfterAsync(input, response);

            _mapper.Map(input, output);

            var userState = new UserState
            {
                IsOldUser = true,
                NextTextIndex = response.NextTextIndex
            };

            var userStateCacheKey = GetCacheKey(response.UserHash);

            await _cache.TryAddAsync(userStateCacheKey, userState);

            return output;
        }

        private IEnumerable<Context> GetContexts(Request input)
        {
            var context = new Context
            {
                Name = input.Payload.Character.Id,
                LifeSpan = 50000
            };

            return new[] { context };
        }

        private string GetCacheKey(string key)
        {
            return $"sber:{key}";
        }
    }
}
