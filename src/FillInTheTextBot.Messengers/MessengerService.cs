using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services;
using NLog;

namespace FillInTheTextBot.Messengers
{
    public abstract class MessengerService<TInput, TOutput> : IMessengerService<TInput, TOutput>
    {
        private const string ErrorAnswer = "Прости, у меня какие-то проблемы... Давай попробуем ещё раз. Если повторится, найди в ВК паблик \"Занимательные истории голосовых помощников\" и напиши об этом в личку";
        private const string ErrorLink = "https://vk.com/fillinthetextbot";

        private readonly IConversationService _conversationService;
        private readonly IMapper _mapper;
        protected readonly IDialogflowService DialogflowService;

        protected readonly Logger Log;

        protected MessengerService(IConversationService conversationService, IMapper mapper, IDialogflowService dialogflowService)
        {
            _conversationService = conversationService;
            _mapper = mapper;
            DialogflowService = dialogflowService;

            Log = LogManager.GetLogger(GetType().Name);
        }

        protected virtual Request Before(TInput input)
        {
            var request = _mapper.Map<Request>(input);

            var contexts = GetContexts(request);
            request.RequiredContexts.AddRange(contexts);

            return request;
        }

        public virtual async Task<TOutput> ProcessIncomingAsync(TInput input)
        {
            Response response;

            try
            {
                var request = Before(input);

                response = ProcessCommand(request);

                if (response == null)
                {
                    response = await _conversationService.GetResponseAsync(request);
                }

                _mapper.Map(request, response);
            }
            catch (Exception e)
            {
                Log.Error(e);

                response = new Response
                {
                    Text = ErrorAnswer,
                    Buttons = new []
                    {
                        new Button
                        {
                            Text = "Сообщить об ошибке",
                            Url = ErrorLink
                        }
                    }
                };
            }

            var output = await AfterAsync(input, response);

            return output;
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
                    Name = $"source-{request.Source?.ToString()}",
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
            }
            catch (Exception e)
            {
                Log.Error(e);
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
            throw new System.NotImplementedException();
        }

        public virtual Task<bool> DeleteWebhookAsync()
        {
            throw new System.NotImplementedException();
        }
    }
}
