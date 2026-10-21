using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Messengers
{
    public abstract class MessengerService<TInput, TOutput> : IMessengerService<TInput, TOutput>
    {
        private const string ErrorAnswer = "Прости, у меня какие-то проблемы... Давай попробуем ещё раз. Если повторится, найди в ВК паблик \"Занимательные истории голосовых помощников\" и напиши об этом в личку";
        private const string ErrorLink = "https://vk.com/fillinthetextbot";

        private readonly IConversationService _conversationService;

        protected readonly ILogger Log;

        protected MessengerService(ILogger log, IConversationService conversationService)
        {
            Log = log;
            _conversationService = conversationService;
        }

        protected virtual Request Before(TInput input)
        {
            throw new NotImplementedException($"Need to implement mapping from {typeof(TInput)} type to " +
                                              $"{typeof(Request)} at overrided '{nameof(Before)}' method of " +
                                              $"{typeof(MessengerService<TInput, TOutput>)} type");
        }

        public virtual async Task<TOutput> ProcessIncomingAsync(TInput input)
        {
            // Ошибка и команда не сбрасывают прогресс ротации
            var requestTextIndex = 0;
            Request request = null;
            Response response;

            try
            {
                using (Tracing.Trace(operationName: "Before"))
                {
                    request = Before(input);
                }

                requestTextIndex = request.NextTextIndex;

                using (Tracing.Trace(a => a
                    .SetTag(nameof(request.UserHash), request.UserHash)
                    .SetTag(nameof(request.SessionId), request.SessionId)))
                {
                    var contexts = GetContexts(request);
                    request.RequiredContexts.AddRange(contexts);

                    response = ProcessCommand(request);

                    if (response == null)
                    {
                        response = await _conversationService.GetResponseAsync(request);
                    }
                    else
                    {
                        response.NextTextIndex = requestTextIndex;
                    }
                }
            }
            catch (Exception e)
            {
                Log.LogError(e, "Error while process incoming");

                response = new Response
                {
                    Text = ErrorAnswer,
                    NextTextIndex = requestTextIndex,
                    Buttons = new[]
                    {
                        new Button
                        {
                            Text = "Сообщить об ошибке",
                            Url = ErrorLink
                        }
                    }
                };

                MetricsCollector.Increment("ErrorAnswer", string.Empty);
            }

            response.RotationState ??= request?.RotationState;

            using (Tracing.Trace(operationName: "AfterAsync"))
            {
                var output = await AfterAsync(input, response);

                return output;
            }
        }

        private ICollection<Context> GetContexts(Request request)
        {
            var contexts = new List<Context>();

            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { nameof(request.UserHash), request.UserHash ?? string.Empty },
                    { nameof(request.ClientId), request.ClientId ?? string.Empty }
                };

                contexts.Add(new Context
                {
                    Name = $"source-{request.Source}",
                    LifeSpan = 2,
                    Parameters = parameters
                });

                if (request.HasScreen)
                {
                    contexts.Add(new Context
                    {
                        Name = "screen",
                        LifeSpan = 2
                    });
                }

                if (request.IsOldUser)
                {
                    contexts.Add(new Context
                    {
                        Name = "OldUser",
                        LifeSpan = 2
                    });
                }
            }
            catch (Exception e)
            {
                Log.LogError(e, "Error while get contexts");
            }

            return contexts;
        }

        protected virtual Response ProcessCommand(Request request)
        {
            return null;
        }

        protected virtual Task<TOutput> AfterAsync(TInput input, Response response)
        {
            throw new NotImplementedException($"Need to implement mapping from {typeof(TInput)} and {typeof(Response)} " +
                                              $"types to " +
                                              $"{typeof(TOutput)} at overrided '{nameof(AfterAsync)}' method of " +
                                              $"{typeof(MessengerService<TInput, TOutput>)} type");
        }

        public virtual Task<bool> SetWebhookAsync(string url)
        {
            throw new NotImplementedException();
        }

        public virtual Task<bool> DeleteWebhookAsync()
        {
            throw new NotImplementedException();
        }
    }
}
