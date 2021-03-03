using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Extensions;
using NLog;

namespace FillInTheTextBot.Messengers
{
    public abstract class MessengerService<TInput, TOutput> : IMessengerService<TInput, TOutput>
    {
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

            return request;
        }

        public virtual async Task<TOutput> ProcessIncomingAsync(TInput input)
        {
            var request = Before(input);

            var response = ProcessCommand(request);

            // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
            if (response == null)
            {
                response = await _conversationService.GetResponseAsync(request);
            }

            TrySetContexts(request);

            _mapper.Map(request, response);

            var output = await AfterAsync(input, response);

            return output;
        }

        private void TrySetContexts(Request request)
        {
            try
            {
                if (request.NewSession != true)
                {
                    return;
                }

                var parameters = new Dictionary<string, string>
                {
                    { nameof(request.UserHash), request.UserHash ?? string.Empty },
                    { nameof(request.ClientId), request.ClientId ?? string.Empty },
                    { nameof(request.Source), request.Source.ToString() }
                };

                request.RequiredContexts.Add(new Context
                {
                    Name = request.Source?.ToString().ToUpper(),
                    LifeSpan = 50000,
                    Parameters = parameters
                });


                request.RequiredContexts.Add(new Context
                {
                    Name = "UserInfo",
                    LifeSpan = 50000,
                    Parameters = parameters
                });

                if (request.HasScreen)
                {
                    request.RequiredContexts.Add(new Context
                    {
                        Name = "screen",
                        LifeSpan = 50000,
                        Parameters = parameters
                    });
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
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
