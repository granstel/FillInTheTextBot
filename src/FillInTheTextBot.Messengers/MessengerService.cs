using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Extensions;

namespace FillInTheTextBot.Messengers
{
    public abstract class MessengerService<TInput, TOutput> : IMessengerService<TInput, TOutput>
    {
        private readonly IConversationService _conversationService;
        private readonly IMapper _mapper;
        protected readonly IDialogflowService DialogflowService;

        protected MessengerService(IConversationService conversationService, IMapper mapper, IDialogflowService dialogflowService)
        {
            _conversationService = conversationService;
            _mapper = mapper;
            DialogflowService = dialogflowService;
        }

        protected virtual Request Before(TInput input)
        {
            var request = _mapper.Map<Request>(input);

            if (request.NewSession == true)
            {
                var parameters = new Dictionary<string, string>
                {
                    { nameof(request.UserHash), request.UserHash ?? string.Empty },
                    { nameof(request.ClientId), request.ClientId ?? string.Empty }
                };

                DialogflowService.SetContextAsync(request.SessionId, "UserInfo", 50000, parameters).Forget();

                if (request.HasScreen)
                {
                    DialogflowService.SetContextAsync(request.SessionId, "screen", 50000).Forget();
                }
            }

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

            _mapper.Map(request, response);

            var output = await AfterAsync(input, response);

            return output;
        }

        protected virtual Response ProcessCommand(Request request)
        {
            return null;
        }

        protected virtual async Task<TOutput> AfterAsync(TInput input, Response response)
        {
            //TODO: проверить, какой тип будет фигурировать в сообщении
            //throw new NotImplementedException($"{nameof(AfterAsync)} doesn't implemented for {GetType().Name}");
            return await Task.Run(() =>
            {
                var output = _mapper.Map<TOutput>(response);

                return output;
            });
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
