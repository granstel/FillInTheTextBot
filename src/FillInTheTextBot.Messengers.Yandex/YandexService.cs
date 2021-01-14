using System;
using System.Threading.Tasks;
using AutoMapper;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Extensions;
using NLog;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Input;

namespace FillInTheTextBot.Messengers.Yandex
{
    public class YandexService : MessengerService<InputModel, OutputModel>, IYandexService
    {
        private const string PingCommand = "ping";
        private const string PongResponse = "pong";
        private const string ErrorCommand = "error";
        private const string IsOldUserOldKey = "isOldUser";

        private const string ErrorAnswer = "Прости, у меня какие-то проблемы... Давай попробуем ещё раз. Если повторится, найди в ВК паблик \"Занимательные истории Алисы из Яндекса\" и напиши об этом в личку";

        private readonly IMapper _mapper;

        private readonly Logger _log = LogManager.GetLogger(nameof(YandexService));

        public YandexService(
            IConversationService conversationService,
            IMapper mapper,
            IDialogflowService dialogflowService) : base(conversationService, mapper, dialogflowService)
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

            
            if (!input.TryGetFromUserState(Models.Request.IsOldUserKey, out bool isOldUser))
            {
                input.TryGetFromUserState(IsOldUserOldKey, out isOldUser);//TODO: remove at next release
            }

            result.IsOldUser = isOldUser;

            if (input.TryGetFromUserState(Models.Response.NextTextIndexKey, out object nextTextIndex) != true)
            {
                input.TryGetFromApplicationState(Models.Response.NextTextIndexKey, out nextTextIndex);
            }

            result.NextTextIndex = Convert.ToInt32(nextTextIndex);

            if (result.NewSession == true)
            {
                SetContexts(input, result);
            }

            return result;
        }

        private void SetContexts(InputModel input, Models.Request request)
        {
            if (input.IsNavigator())
            {
                DialogflowService.SetContextAsync(request.SessionId, "navigator", 50000).Forget();
            }

            if (input.IsCanShowAdvertising())
            {
                DialogflowService.SetContextAsync(request.SessionId, "advertising", 50000).Forget();
            }
        }

        protected override Models.Response ProcessCommand(Models.Request request)
        {
            Models.Response response = null;

            if (PingCommand.Equals(request.Text, StringComparison.InvariantCultureIgnoreCase))
            {
                response = new Models.Response { Text = PongResponse };
            }

            if (ErrorCommand.Equals(request.Text, StringComparison.InvariantCultureIgnoreCase))
            {
                response = new Models.Response { Text = ErrorAnswer };
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

                var response = new Models.Response { Text = ErrorAnswer };

                result = await AfterAsync(input, response);
            }

            return result;
        }

        protected override async Task<OutputModel> AfterAsync(InputModel input, Models.Response response)
        {
            var output = await base.AfterAsync(input, response);

            _mapper.Map(input, output);

            output.AddToUserState(IsOldUserOldKey, null);//TODO: remove at next release
            output.AddToUserState(Models.Request.IsOldUserKey, true);
            output.AddToUserState(Models.Response.NextTextIndexKey, response.NextTextIndex);
            output.AddToApplicationState(Models.Response.NextTextIndexKey, response.NextTextIndex);

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
