using AutoMapper;
using FillInTheTextBot.Services;
using NLog;
using Sber.SmartApp.Models;

namespace FillInTheTextBot.Messengers.Sber
{
    public class SberService : MessengerService<Request, Response>, ISberService
    {
        private const string ErrorAnswer = "Прости, у меня какие-то проблемы... Давай попробуем ещё раз. Если повторится, найди в ВК паблик \"Занимательные истории Алисы из Яндекса\" и напиши об этом в личку";

        private readonly IMapper _mapper;

        private readonly Logger _log = LogManager.GetLogger(nameof(SberService));

        public SberService(
            IConversationService conversationService,
            IMapper mapper,
            IDialogflowService dialogflowService) : base(conversationService, mapper, dialogflowService)
        {
            _mapper = mapper;
        }
    }
}
