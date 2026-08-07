using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Services.BackgroundTasks
{
    public sealed class BackgroundTaskProcessor : BackgroundService
    {
        /// <summary>
        /// Сколько работ выполняется одновременно. Прежний fire-and-forget запускал их
        /// без ограничений, и медленная работа не задерживала остальные — это свойство
        /// нужно сохранить. Ограничение не даёт при всплеске открыть неограниченное
        /// число обращений к Redis и Dialogflow.
        /// </summary>
        public const int MaxDegreeOfParallelism = 4;

        private readonly BackgroundTaskQueue _queue;
        private readonly ILogger<BackgroundTaskProcessor> _log;

        public BackgroundTaskProcessor(BackgroundTaskQueue queue, ILogger<BackgroundTaskProcessor> log)
        {
            _queue = queue;
            _log = log;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return DrainAsync();
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _queue.Complete();

            await base.StopAsync(cancellationToken);

            // BackgroundService запускает ExecuteAsync отложенно, и при остановке вскоре
            // после старта задача отменяется до входа в тело метода — тогда очередь никто
            // не разобрал. Поэтому остаток добирается здесь, независимо от того,
            // успел ли стартовать основной цикл.
            await DrainAsync();
        }

        private Task DrainAsync()
        {
            // Несколько независимых потребителей одного канала. Parallel.ForEachAsync здесь
            // не подходит: при остановке его задача завершается как отменённая, не дочитав
            // очередь, и принятые работы теряются.
            var consumers = Enumerable
                .Range(0, MaxDegreeOfParallelism)
                .Select(_ => ConsumeAsync());

            return Task.WhenAll(consumers);
        }

        /// <summary>
        /// Читает очередь, пока она не закрыта на запись и не разобрана. Отмена по
        /// stoppingToken намеренно не используется: при остановке приложения уже принятые
        /// работы должны быть доведены до конца, а не потеряны вместе с процессом.
        /// </summary>
        private async Task ConsumeAsync()
        {
            await foreach (var task in _queue.ReadAllAsync())
            {
                await ExecuteTaskAsync(task);
            }
        }

        private async Task ExecuteTaskAsync(BackgroundTask task)
        {
            try
            {
                await task.Work().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _log.LogError(e, "Error while executing background task '{TaskName}'", task.Name);
            }
        }
    }
}
