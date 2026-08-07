using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Services.BackgroundTasks
{
    public sealed class BackgroundTaskProcessor : BackgroundService
    {
        /// <summary>
        /// Работы в очереди независимы, поэтому выполняются параллельно — как это было
        /// с прежним fire-and-forget. Ограничение не даёт при всплеске открыть
        /// неограниченное число обращений к Redis и Dialogflow.
        /// </summary>
        public const int MaxDegreeOfParallelism = 4;

        private readonly BackgroundTaskQueue _queue;
        private readonly ILogger<BackgroundTaskProcessor> _log;

        public BackgroundTaskProcessor(BackgroundTaskQueue queue, ILogger<BackgroundTaskProcessor> log)
        {
            _queue = queue;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = MaxDegreeOfParallelism };

            // Чтение намеренно не отменяется по stoppingToken: цикл заканчивается, когда
            // очередь закрыта на запись и разобрана. Так при остановке приложения уже
            // принятые работы доводятся до конца, а не теряются
            await Parallel.ForEachAsync(_queue.ReadAllAsync(), options, (task, _) => ExecuteTaskAsync(task));
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _queue.Complete();

            await base.StopAsync(cancellationToken);
        }

        private async ValueTask ExecuteTaskAsync(BackgroundTask task)
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
