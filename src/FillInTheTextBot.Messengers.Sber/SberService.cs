using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Extensions;
using GranSteL.Helpers.Redis;
using Microsoft.Extensions.Logging;
using Request = Sber.SmartApp.Models.Request;
using Response = Sber.SmartApp.Models.Response;

namespace FillInTheTextBot.Messengers.Sber;

public class SberService(
    ILogger<SberService> log,
    IConversationService conversationService,
    IRedisCacheService cache)
    : MessengerService<Request, Response>(log, conversationService), ISberService
{
    protected override Models.Request Before(Request input)
    {
        var request = input.ToRequest();

        var userStateCacheKey = GetCacheKey(request.UserHash);

        cache.TryGet(userStateCacheKey, out UserState userState);

        request.IsOldUser = userState?.IsOldUser ?? false;

        request.NextTextIndex = Convert.ToInt32(userState?.NextTextIndex ?? 0);

        request.ScopeKey = userState?.ScopeKey;

        var contexts = GetContexts(input);
        request.RequiredContexts.AddRange(contexts);

        request.SessionId = TryGetSessionIdAsync(request.NewSession, request.UserHash);

        return request;
    }

    protected override async Task<Response> AfterAsync(Request input, Models.Response response)
    {
        var output = response.ToResponse();

        output = input.FillResponse(output);

        // TODO: ScopeKey должен меняться с сессией, надо сделать SessionState. И использовать его
        // в других мессенджерах вместе с UserState
        var userState = new UserState
        {
            IsOldUser = true,
            NextTextIndex = response.NextTextIndex,
            ScopeKey = response.ScopeKey
        };

        var userStateCacheKey = GetCacheKey(input.Uuid?.Sub ?? input.Uuid?.UserId);

        await cache.TryAddAsync(userStateCacheKey, userState, TimeSpan.FromDays(14));

        return output;
    }

    private string TryGetSessionIdAsync(bool? newSession, string userHash)
    {
        var cacheKey = GetSessionCacheKey(userHash);

        cache.TryGet(cacheKey, out string sessionId);

        // TODO: создавать новую сессию не в этом методе (get)
        if (newSession == true || string.IsNullOrEmpty(sessionId))
        {
            sessionId = Guid.NewGuid().ToString("N");

            cache.AddAsync(cacheKey, sessionId, TimeSpan.FromMinutes(5)).Forget();
        }

        return sessionId;
    }

    private IEnumerable<Context> GetContexts(Request input)
    {
        var contexts = new List<Context>();

        contexts.Add(new Context
        {
            Name = $"sber-character-{input.Payload?.Character?.Id}",
            LifeSpan = 2
        });

        var appeal = input.Payload?.Character?.Appeal;

        if (!string.IsNullOrEmpty(appeal))
            contexts.Add(new Context
            {
                Name = appeal,
                LifeSpan = 2
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