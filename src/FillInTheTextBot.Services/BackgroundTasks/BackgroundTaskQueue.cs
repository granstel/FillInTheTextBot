using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Services.BackgroundTasks
{
    public sealed class BackgroundTaskQueue : IBackgroundTaskQueue, IBackgroundTaskReader
    {
        /// <summary>
        /// Ёмкость подобрана с запасом на всплеск запросов: при штатной нагрузке очередь
        /// разбирается быстрее, чем наполняется, а при недоступности внешнего сервиса
        /// ограничение не даёт очереди съесть память.
        /// </summary>
        public const int Capacity = 1000;

        private readonly Channel<BackgroundTask> _channel;
        private readonly ILogger<BackgroundTaskQueue> _log;

        public BackgroundTaskQueue(ILogger<BackgroundTaskQueue> log)
        {
            _log = log;

            // Режим Wait выбран ради TryWrite: на заполненной очереди он возвращает false,
            // не блокируя вызывающий поток, и переполнение видно в логе. Режим DropWrite
            // молча отбрасывал бы работу и возвращал true
            var options = new BoundedChannelOptions(Capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            };

            _channel = Channel.CreateBounded<BackgroundTask>(options);
        }

        public bool Enqueue(string name, Func<Task> work)
        {
            if (work is null)
            {
                return false;
            }

            var task = new BackgroundTask(name, work);

            var written = _channel.Writer.TryWrite(task);

            if (!written)
            {
                _log.LogWarning("Background task queue is full, task '{TaskName}' is dropped", name);
            }

            return written;
        }

        public IAsyncEnumerable<BackgroundTask> ReadAllAsync()
        {
            return _channel.Reader.ReadAllAsync();
        }

        public void Complete()
        {
            _channel.Writer.TryComplete();
        }
    }
}
