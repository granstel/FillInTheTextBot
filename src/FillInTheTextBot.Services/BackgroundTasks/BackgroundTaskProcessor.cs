using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Services.BackgroundTasks
{
    public sealed class BackgroundTaskProcessor(IBackgroundTaskReader queue, ILogger<BackgroundTaskProcessor> log)
        : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return DrainAsync();
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            queue.Complete();

            await base.StopAsync(cancellationToken);

            // BackgroundService запускает ExecuteAsync отложенно, и при остановке вскоре
            // после старта задача отменяется до входа в тело метода — тогда очередь никто
            // не разобрал. Поэтому остаток добирается здесь, независимо от того,
            // успел ли стартовать основной цикл.
            await DrainAsync();
        }

        /// <summary>
        /// Читает очередь и запускает каждую работу, не дожидаясь её завершения:
        /// параллелизм не ограничен, как в прежнем fire-and-forget, и медленная или
        /// зависшая работа не задерживает остальные. Отмена по stoppingToken намеренно
        /// не используется — при остановке приложения уже принятые работы должны быть
        /// доведены до конца, поэтому после закрытия очереди метод дожидается запущенных.
        /// </summary>
        private async Task DrainAsync()
        {
            var running = new ConcurrentDictionary<Task, byte>();

            await foreach (var task in queue.ReadAllAsync())
            {
                var work = ExecuteTaskAsync(task);

                // Синхронно завершившиеся работы (Task.CompletedTask и т.п.) не трекаем
                if (work.IsCompleted)
                {
                    continue;
                }

                // Трекаем работы «в полёте», чтобы дождаться их при остановке. Работа
                // сама убирает себя из набора по завершении, иначе он рос бы бесконечно.
                running[work] = 0;
                _ = work.ContinueWith(
                    finished => running.TryRemove(finished, out _),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            await Task.WhenAll(running.Keys);
        }

        private async Task ExecuteTaskAsync(BackgroundTask task)
        {
            try
            {
                await task.Work().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                log.LogError(e, "Error while executing background task '{TaskName}'", task.Name);
            }
        }
    }
}
