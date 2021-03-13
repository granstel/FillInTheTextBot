using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Extensions;
using GranSteL.Helpers.Redis;
using NLog;
using Sber.SmartApp.Models;

namespace FillInTheTextBot.Messengers.Sber
{
    public class SberService : MessengerService<Request, Response>, ISberService
    {
        private const string ErrorAnswer = "Прости, у меня какие-то проблемы... Давай попробуем ещё раз. Если повторится, найди в ВК паблик \"Занимательные истории голосовых помощников\" и напиши об этом в личку";

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
            //if (input == default)
            //{
            //    _log.Error($"{nameof(Request)} is null");

            //    input = CreateErrorInput();
            //}

            var request = base.Before(input);

            var userStateCacheKey = GetCacheKey(request.UserHash);

            _cache.TryGet(userStateCacheKey, out UserState userState);

            request.IsOldUser = userState?.IsOldUser ?? false;

            request.NextTextIndex = Convert.ToInt32(userState?.NextTextIndex ?? 0);

            var contexts = GetContexts(input);
            request.RequiredContexts.AddRange(contexts);

            request.SessionId = TryGetSessionIdAsync(request.NewSession, request.UserHash);

            return request;
        }

        public override async Task<Response> ProcessIncomingAsync(Request input)
        {
            Response result;

            try
            {
                result = await base.ProcessIncomingAsync(input);
            }
            catch (Exception e)
            {
                _log.Error(e);

                var response = new Models.Response { Text = ErrorAnswer };

                result = await AfterAsync(input, response);
            }

            return result;
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

            await _cache.TryAddAsync(userStateCacheKey, userState, TimeSpan.FromDays(14));

            return output;
        }

        private string TryGetSessionIdAsync(bool? newSession, string userHash)
        {
            var cacheKey = GetSessionCacheKey(userHash);

            _cache.TryGet(cacheKey, out string sessionId);

            if (newSession == true || string.IsNullOrEmpty(sessionId))
            {
                sessionId = Guid.NewGuid().ToString("N");

                _cache.AddAsync(cacheKey, sessionId, TimeSpan.FromMinutes(5)).Forget();
            }

            return sessionId;
        }

        private IEnumerable<Models.Context> GetContexts(Request input)
        {
            var contexts = new List<Models.Context>();

            contexts.Add(new Models.Context
            {
                Name = $"sber-character-{input.Payload?.Character?.Id}",
                LifeSpan = 50000
            });

            contexts.Add(new Models.Context
            {
                Name = input.Payload?.Character?.Appeal,
                LifeSpan = 50000
            });

            return contexts;
        }

        private string GetCacheKey(string key)
        {
            return $"sber:{key}";
        }

        private string GetSessionCacheKey(string key)
        {
            return $"sber:session:{key}";
        }
    }
}
