using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Extensions;
using NLog;
using Yandex.Dialogs.Models;
using Yandex.Dialogs.Models.Input;
using Request = Yandex.Dialogs.Models.Request;

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

            var request = base.Before(input);


            input.TryGetFromUserState(Models.Request.IsOldUserKey, out bool isOldUser);

            request.IsOldUser = isOldUser;

            if (input.TryGetFromUserState(Models.Response.NextTextIndexStorageKey, out object nextTextIndex) != true)
            {
                input.TryGetFromApplicationState(Models.Response.NextTextIndexStorageKey, out nextTextIndex);
            }

            request.NextTextIndex = Convert.ToInt32(nextTextIndex);

            input.TryGetFromSessionState(Models.Response.ScopeStorageKey, out string scopeKey);

            request.ScopeKey = scopeKey;

            if (request.NewSession == true)
            {
                var contexts = GetContexts(input);

                request.RequiredContexts.AddRange(contexts);
            }

            return request;
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

            output.AddToUserState(Models.Request.IsOldUserKey, true);

            output.AddToUserState(Models.Response.NextTextIndexStorageKey, response.NextTextIndex);
            output.AddToApplicationState(Models.Response.NextTextIndexStorageKey, response.NextTextIndex);

            output.AddToSessionState(Models.Response.ScopeStorageKey, response.ScopeKey);

            return output;
        }

        private ICollection<Context> GetContexts(InputModel input)
        {
            var contexts = new List<Context>();

            if (input.IsNavigator())
            {
                contexts.Add(new Context
                {
                    Name = "navigator",
                    LifeSpan = 50000
                });
            }

            if (input.IsCanShowAdvertising())
            {
                contexts.Add(new Context
                {
                    Name = "advertising",
                    LifeSpan = 50000
                });
            }

            return contexts;
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
