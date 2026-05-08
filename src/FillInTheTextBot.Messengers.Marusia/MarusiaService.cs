using System;
using System.Threading.Tasks;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Extensions;
using GranSteL.Helpers.Redis;
using MailRu.Marusia.Models;
using MailRu.Marusia.Models.Input;
using Microsoft.Extensions.Logging;
using Request = FillInTheTextBot.Models.Request;
using Response = FillInTheTextBot.Models.Response;

namespace FillInTheTextBot.Messengers.Marusia;

public class MarusiaService(
    ILogger<MarusiaService> log,
    IConversationService conversationService,
    IRedisCacheService cache)
    : MessengerService<InputModel, OutputModel>(log, conversationService), IMarusiaService
{
    private const string PingCommand = "ping";
    private const string PongResponse = "pong";

    protected override Request Before(InputModel input)
    {
        var request = input.ToRequest();

        input.TryGetFromUserState(Request.IsOldUserKey, out bool isOldUser);

        request.IsOldUser = isOldUser;

        input.TryGetFromUserState(Response.NextTextIndexStorageKey, out object nextTextIndex);

        request.NextTextIndex = Convert.ToInt32(nextTextIndex);

        input.TryGetFromSessionState(Response.ScopeStorageKey, out string scopeKey);

        request.ScopeKey = scopeKey;

        return request;
    }

    protected override Response ProcessCommand(Request request)
    {
        Response response = null;

        if (PingCommand.Equals(request.Text, StringComparison.InvariantCultureIgnoreCase))
            response = new Response { Text = PongResponse };

        return response;
    }

    protected override Task<OutputModel> AfterAsync(InputModel input, Response response)
    {
        var output = response.ToOutput();

        output = input.FillOutput(output);

        output.AddToUserState(Request.IsOldUserKey, true);

        output.AddToUserState(Response.NextTextIndexStorageKey, response.NextTextIndex);

        output.AddToSessionState(Response.ScopeStorageKey, response.ScopeKey);

        cache.AddAsync($"marusia:{input.Session?.UserId}", string.Empty, TimeSpan.FromDays(14)).Forget();

        return Task.FromResult(output);
    }
}