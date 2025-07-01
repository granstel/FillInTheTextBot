using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services;
using Microsoft.Extensions.Logging;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Input;
using Request = FillInTheTextBot.Models.Request;
using Response = FillInTheTextBot.Models.Response;

namespace FillInTheTextBot.Messengers.Yandex;

public class YandexService : MessengerService<InputModel, OutputModel>, IYandexService
{
    private const string PingCommand = "ping";
    private const string PongResponse = "pong";

    public YandexService(
        ILogger<YandexService> log,
        IConversationService conversationService) : base(log, conversationService)
    {
    }

    protected override Request Before(InputModel input)
    {
        var request = input.ToRequest();

        input.TryGetFromUserState(Request.IsOldUserKey, out bool isOldUser);

        request.IsOldUser = isOldUser;

        if (input.TryGetFromUserState(Response.NextTextIndexStorageKey, out object nextTextIndex) != true)
            input.TryGetFromApplicationState(Response.NextTextIndexStorageKey, out nextTextIndex);

        request.NextTextIndex = Convert.ToInt32(nextTextIndex);

        input.TryGetFromSessionState(Response.ScopeStorageKey, out string scopeKey);

        request.ScopeKey = scopeKey;

        var contexts = GetContexts(input);

        request.RequiredContexts.AddRange(contexts);

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
        output.AddToApplicationState(Response.NextTextIndexStorageKey, response.NextTextIndex);

        output.AddToSessionState(Response.ScopeStorageKey, response.ScopeKey);

        output.AddAnalyticsEvent(Response.ScopeStorageKey,
            new Dictionary<string, object> { { Response.ScopeStorageKey, response.ScopeKey } });

        return Task.FromResult(output);
    }

    private ICollection<Context> GetContexts(InputModel input)
    {
        var contexts = new List<Context>();

        if (input.IsNavigator())
            contexts.Add(new Context
            {
                Name = "navigator",
                LifeSpan = 2
            });

        if (input.IsCanShowAdvertising())
            contexts.Add(new Context
            {
                Name = "advertising",
                LifeSpan = 2
            });

        return contexts;
    }
}