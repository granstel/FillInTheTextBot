using FillInTheTextBot.Services;
using FillInTheTextBot.Services.Configuration;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Messengers.Tests.Fixtures;

public class ControllerFixture(
    ILogger<ControllerFixture> log,
    IMessengerService<InputFixture, OutputFixture> messengerService,
    MessengerConfiguration configuration)
    : MessengerController<InputFixture, OutputFixture>(log, messengerService, configuration);