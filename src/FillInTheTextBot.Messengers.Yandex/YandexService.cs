using System;
using System.Threading.Tasks;
using AutoMapper;
using FillInTheTextBot.Models;
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
        private const string ErrorCommand = "error";

        private const string oldUSerStateKey = "isOldUser";

        private const string errorAnswer = "Простите, у меня какие-то проблемы... Давайте попробуем ещё раз";

        private readonly IMapper _mapper;

        private readonly Logger _log = LogManager.GetLogger(nameof(YandexService));

        public YandexService(IConversationService conversationService, IMapper mapper) : base(conversationService, mapper)
        {
            _mapper = mapper;
        }

        protected override Models.Request Before(InputModel input)
        {
            if (input == default)
            {
                _log.Error($"{nameof(InputModel)} is null");

                input = CreateErrorInput();
            }

            var result = base.Before(input);

            if (input.TryGetFromUserState(oldUSerStateKey, out bool IsOldUser))
            {
                result.IsOldUser = IsOldUser;
            }

            return result;
        }

        protected override Models.Response ProcessCommand(Models.Request request)
        {
            Models.Response response = null;

            if (PingCommand.Equals(request.Text, StringComparison.InvariantCultureIgnoreCase))
            {
                response = new Models.Response { Text = PongResponse };
            }

            if (ErrorCommand.Equals(request?.Text, StringComparison.InvariantCultureIgnoreCase))
            {
                response = new Models.Response { Text = errorAnswer };
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

                var response = new Models.Response { Text = errorAnswer };

                result = await AfterAsync(input, response);
            }

            return result;
        }

        protected override async Task<OutputModel> AfterAsync(InputModel input, Models.Response response)
        {
            var output = await base.AfterAsync(input, response);

            _mapper.Map(input, output);

            output.AddToUserState(oldUSerStateKey, true);

            return output;
        }

        private InputModel CreateErrorInput()
        {
            return new InputModel
            {
                Request = new Request
                {
                    OriginalUtterance = ErrorCommand
                },
                Session = new InputSession(),
                Version = "1.0"
            };
        }
    }
}
