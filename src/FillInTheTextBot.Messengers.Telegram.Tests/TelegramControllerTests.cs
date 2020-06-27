﻿using System.Threading.Tasks;
using AutoFixture;
using FillInTheTextBot.Messengers.Tests.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using Telegram.Bot.Types;

namespace FillInTheTextBot.Messengers.Telegram.Tests
{
    [TestFixture]
    public class TelegramControllerTests : ControllerTests<TelegramController>
    {
        private Mock<ITelegramService> _telegramService;
        private Mock<TelegramConfiguration> _configuration;

        [SetUp]
        public void InitTest()
        {
            InitTestBase();

            _telegramService = MockRepository.Create<ITelegramService>();
            _configuration = MockRepository.Create<TelegramConfiguration>();

            Target = new TelegramController(_telegramService.Object, _configuration.Object);
        }

        [Test]
        public async Task TestTelegramApi_TestApiAsync_Response()
        {
            var expected = true;

            _telegramService.Setup(s => s.TestApiAsync()).ReturnsAsync(expected);


            var result = await Target.TestTelegramApiAsync();


            MockRepository.VerifyAll();

            var value = (result as JsonResult)?.Value;
            Assert.AreEqual(expected, value);
        }


        [Test]
        public async Task GetMeAsync_GetMeAsync_Response()
        {
            var user = Fixture.Create<User>();

            _telegramService.Setup(s => s.GetMeAsync()).ReturnsAsync(user);


            var result = await Target.GetMeAsync();


            MockRepository.VerifyAll();

            var value = (result as JsonResult)?.Value;
            Assert.AreEqual(user, value);
        }
    }
}
