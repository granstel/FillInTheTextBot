using System;
using System.Threading.Tasks;
using AutoFixture;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services.BackgroundTasks;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.Rotation;
using FillInTheTextBot.Services.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests
{
    /// <summary>
    /// Характеризующие тесты: фиксируют текущее поведение <see cref="ConversationService"/>
    /// перед обновлением фреймворка и пакетов.
    /// </summary>
    [TestFixture]
    public partial class ConversationServiceTests
    {
        private Mock<IDialogflowService> _dialogflowService;
        private Mock<IRedisCacheService> _cache;
        private Mock<IBackgroundTaskQueue> _backgroundTasks;

        private ConversationConfiguration _configuration;
        private RotationStore _rotationStore;

        private ConversationService _target;

        private Fixture _fixture;

        [SetUp]
        public void InitTest()
        {
            _dialogflowService = new Mock<IDialogflowService>();
            _cache = new Mock<IRedisCacheService>();

            _backgroundTasks = new Mock<IBackgroundTaskQueue>();

            // Фоновые работы выполняются сразу, чтобы тест проверял результат, а не гонку
            _backgroundTasks
                .Setup(q => q.Enqueue(It.IsAny<string>(), It.IsAny<Func<Task>>()))
                .Returns((string _, Func<Task> work) =>
                {
                    work().GetAwaiter().GetResult();
                    return true;
                });

            _configuration = new ConversationConfiguration();

            _rotationStore = new RotationStore();

            var storyRotation = new StoryRotation(_rotationStore, new RotationConfiguration(), new Random(1), TimeProvider.System);

            _target = new ConversationService(_configuration, _dialogflowService.Object, _cache.Object, _backgroundTasks.Object,
                storyRotation, NullLogger<ConversationService>.Instance);

            _fixture = new Fixture { OmitAutoProperties = true };
        }

        private void SetupDialog(Dialog dialog)
        {
            _dialogflowService.Setup(s => s.GetResponseAsync(It.IsAny<Request>())).ReturnsAsync(dialog);
        }
    }
}
