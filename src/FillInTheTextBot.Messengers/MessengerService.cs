using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Mapping;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Messengers
{
    public abstract class MessengerService<TInput, TOutput> : IMessengerService<TInput, TOutput>
    {
        private const string ErrorAnswer = "Прости, у меня какие-то проблемы... Давай попробуем ещё раз. Если повторится, найди в ВК паблик \"Занимательные истории голосовых помощников\" и напиши об этом в личку";
        private const string ErrorLink = "https://vk.com/fillinthetextbot";

        private readonly IConversationService _conversationService;
        private readonly IMapper _mapper;

        protected readonly ILogger Log;

        protected MessengerService(ILogger log, IConversationService conversationService, IMapper mapper)
        {
            Log = log;
            _conversationService = conversationService;
            _mapper = mapper;
        }

        protected virtual Request Before(TInput input)
        {
            var request = _mapper.Map<Request>(input);

            return request;
        }

        public virtual async Task<TOutput> ProcessIncomingAsync(TInput input)
        {
            Response response;

            try
            {
                Request request;
                
                using (Tracing.Trace(operationName: "Before"))
                {
                    request = Before(input);
                }

                using (Tracing.Trace(s => s
                    .WithTag(nameof(request.UserHash), request.UserHash)
                    .WithTag(nameof(request.SessionId), request.SessionId)))
                {
                    var contexts = GetContexts(request);
                    request.RequiredContexts.AddRange(contexts);

                    response = ProcessCommand(request);

                    if (response == null)
                    {
                        response = await _conversationService.GetResponseAsync(request);
                    }

                    using (Tracing.Trace(operationName: "Map request to response"))
                    {
                        response = request.ToResponse(response);
                    }
                }
            }
            catch (Exception e)
            {
                Log.LogError(e, "Error while process incoming");

                response = new Response
                {
                    Text = ErrorAnswer,
                    Buttons = new[]
                    {
                        new Button
                        {
                            Text = "Сообщить об ошибке",
                            Url = ErrorLink
                        }
                    }
                };
            }

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

        protected virtual async Task<TOutput> AfterAsync(TInput input, Response response)
        {
            var output = _mapper.Map<TOutput>(response);

            return await Task.FromResult(output);
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
