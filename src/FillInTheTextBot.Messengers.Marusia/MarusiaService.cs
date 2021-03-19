using System;
using System.Threading.Tasks;
using AutoMapper;
using FillInTheTextBot.Services;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Input;

namespace FillInTheTextBot.Messengers.Marusia
{
    public class MarusiaService : MessengerService<InputModel, OutputModel>, IMarusiaService
    {
        private const string PingCommand = "ping";
        private const string PongResponse = "pong";

        private readonly IMapper _mapper;

        public MarusiaService(
            IConversationService conversationService,
            IMapper mapper,
            IDialogflowService dialogflowService) : base(conversationService, mapper, dialogflowService)
        {
            _mapper = mapper;
        }

        protected override Models.Request Before(InputModel input)
        {
            var request = base.Before(input);

            return request;
        }

        protected override Models.Response ProcessCommand(Models.Request request)
        {
            Models.Response response = null;

            if (PingCommand.Equals(request.Text, StringComparison.InvariantCultureIgnoreCase))
            {
                response = new Models.Response { Text = PongResponse };
            }

            return response;
        }

        protected override async Task<OutputModel> AfterAsync(InputModel input, Models.Response response)
        {
            var output = await base.AfterAsync(input, response);

            _mapper.Map(input, output);

            return output;
        }
    }
}
