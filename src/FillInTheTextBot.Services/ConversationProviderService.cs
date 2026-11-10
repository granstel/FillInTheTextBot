using System;
using System.Threading.Tasks;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services.Configuration;

namespace FillInTheTextBot.Services;

public sealed class ConversationProviderService(ConversationConfiguration configuration, Func<IConversationService> createProvider)
    : IConversationService
{
    public async Task<Response> GetResponseAsync(Request request)
    {
        var selected = configuration.Provider.ToString();
        if (request.NewSession == true)
        {
            request.ConversationProvider = null;
            request.ConversationState = null;
            request.ScopeKey = null;
        }

        var previous = request.ConversationProvider;
        if (string.IsNullOrEmpty(previous) && !string.IsNullOrEmpty(request.ScopeKey))
        {
            previous = nameof(ConversationProvider.Dialogflow);
        }

        if (!string.IsNullOrEmpty(previous) && !string.Equals(previous, selected, StringComparison.Ordinal))
        {
            if (previous is not ("Dialogflow" or "StoryDialog"))
            {
                request.ConversationProvider = null;
                request.ConversationState = null;
            }
            else if (request.ConversationState is not null &&
                     (request.ConversationState.Length > 700 || System.Text.Encoding.UTF8.GetByteCount(request.ConversationState) > 700))
            {
                request.ConversationState = null;
            }
            var blocked = new Response
            {
                Text = "Настройки навыка изменились. Начните новую сессию навыка, чтобы продолжить.",
                NextTextIndex = request.NextTextIndex,
                RotationState = request.RotationState,
                ScopeKey = request.ScopeKey,
                ConversationProvider = previous is "Dialogflow" or "StoryDialog" ? previous : null,
                ConversationState = request.ConversationState
            };
            return blocked;
        }

        request.ConversationProvider = selected;
        if (configuration.Provider == ConversationProvider.Dialogflow)
        {
            request.ConversationState = null;
        }
        var provider = createProvider();
        var response = await provider.GetResponseAsync(request).ConfigureAwait(false);
        response.ConversationProvider = selected;
        response.ConversationState ??= request.ConversationState;
        return response;
    }
}
