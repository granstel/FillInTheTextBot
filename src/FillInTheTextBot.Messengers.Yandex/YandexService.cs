using System;
using System.Threading.Tasks;
using AutoMapper;
using FillInTheTextBot.Services;
using NLog;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Input;
using Internal = FillInTheTextBot.Models.Internal;

namespace FillInTheTextBot.Messengers.Yandex
{
    public class YandexService : MessengerService<InputModel, OutputModel>, IYandexService
    {
        private const string PingCommand = "ping";
        private const string PongResponse = "pong";

        private readonly IMapper _mapper;

        private readonly Logger _log = LogManager.GetLogger(nameof(YandexService));

        public YandexService(IConversationService conversationService, IMapper mapper) : base(conversationService, mapper)
        {
            _mapper = mapper;
        }

        protected override Internal.Response ProcessCommand(Internal.Request request)
        {
            Internal.Response response = null;

            if (PingCommand.Equals(request.Text, StringComparison.InvariantCultureIgnoreCase))
            {
                response = new Internal.Response { Text = PongResponse };
            }

            return response;
        }

        public override async Task<OutputModel> ProcessIncomingAsync(InputModel input)
        {
            OutputModel result;

            try
            {
                result = await base.ProcessIncomingAsync(input);
            }
            catch (Exception e)
            {
                _log.Error(e);

                var response = new Internal.Response { Text = "Простите, у меня какие-то проблемы... Давайте попробуем ещё раз" };

                result = await AfterAsync(input, response);
            }

            return result;
        }

        protected override async Task<OutputModel> AfterAsync(InputModel input, Internal.Response response)
        {
            var output = await base.AfterAsync(input, response);

            _mapper.Map(input, output);

            return output;
        }
    }
}
