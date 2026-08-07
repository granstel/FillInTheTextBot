using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FillInTheTextBot.Services.BackgroundTasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace FillInTheTextBot.Services.Tests.BackgroundTasks
{
    [TestFixture]
    public class BackgroundTaskProcessorTests
    {
        private BackgroundTaskQueue _queue;
        private BackgroundTaskProcessor _target;

        [SetUp]
        public void InitTest()
        {
            _queue = new BackgroundTaskQueue(NullLogger<BackgroundTaskQueue>.Instance);
            _target = new BackgroundTaskProcessor(_queue, NullLogger<BackgroundTaskProcessor>.Instance);
        }

        [TearDown]
        public async Task CleanUp()
        {
            await _target.StopAsync(CancellationToken.None);
            _target.Dispose();
        }

        [Test]
        public async Task Enqueue_Work_Executed()
        {
            var executed = new TaskCompletionSource<bool>();

            await _target.StartAsync(CancellationToken.None);

            var accepted = _queue.Enqueue("проверка", () =>
            {
                executed.TrySetResult(true);
                return Task.CompletedTask;
            });


            var completed = await Task.WhenAny(executed.Task, Task.Delay(5000));


            ClassicAssert.True(accepted);
            ClassicAssert.AreSame(executed.Task, completed, "Работа из очереди должна быть выполнена");
        }

        [Test]
        public async Task Enqueue_ManyWorks_AllExecuted()
        {
            const int count = 50;

            var executed = new ConcurrentBag<int>();

            await _target.StartAsync(CancellationToken.None);

            foreach (var i in Enumerable.Range(0, count))
            {
                var number = i;

                _queue.Enqueue($"работа-{number}", () =>
                {
                    executed.Add(number);
                    return Task.CompletedTask;
                });
            }


            await WaitForAsync(() => executed.Count == count);


            ClassicAssert.AreEqual(count, executed.Count);
            CollectionAssert.AreEquivalent(Enumerable.Range(0, count), executed);
        }

        [Test]
        public async Task Enqueue_FailingWork_QueueKeepsWorking()
        {
            var executed = new TaskCompletionSource<bool>();

            await _target.StartAsync(CancellationToken.None);

            _queue.Enqueue("падающая", () => throw new InvalidOperationException("ошибка внутри работы"));

            _queue.Enqueue("следующая", () =>
            {
                executed.TrySetResult(true);
                return Task.CompletedTask;
            });


            var completed = await Task.WhenAny(executed.Task, Task.Delay(5000));


            ClassicAssert.AreSame(executed.Task, completed, "Исключение в одной работе не должно останавливать обработчик");
        }

        [Test]
        public async Task StopAsync_PendingWork_Executed()
        {
            var executed = new TaskCompletionSource<bool>();

            await _target.StartAsync(CancellationToken.None);

            _queue.Enqueue("до остановки", () =>
            {
                executed.TrySetResult(true);
                return Task.CompletedTask;
            });


            await _target.StopAsync(CancellationToken.None);


            var completed = await Task.WhenAny(executed.Task, Task.Delay(5000));

            ClassicAssert.AreSame(executed.Task, completed, "Принятые работы должны успеть выполниться при остановке");
        }

        [Test]
        public void Enqueue_QueueIsFull_TaskDropped()
        {
            // Обработчик не запущен, поэтому очередь только наполняется
            foreach (var i in Enumerable.Range(0, BackgroundTaskQueue.Capacity))
            {
                var accepted = _queue.Enqueue($"работа-{i}", () => Task.CompletedTask);

                ClassicAssert.True(accepted, $"Работа {i} должна помещаться в очередь");
            }


            var overflow = _queue.Enqueue("лишняя", () => Task.CompletedTask);


            ClassicAssert.False(overflow, "Переполнение очереди не должно блокировать вызывающий поток");
        }

        [Test]
        public void Enqueue_NullWork_NotAccepted()
        {
            var accepted = _queue.Enqueue("пустая", null);

            ClassicAssert.False(accepted);
        }

        private static async Task WaitForAsync(Func<bool> condition, int timeoutMilliseconds = 5000)
        {
            var waited = 0;

            while (!condition() && waited < timeoutMilliseconds)
            {
                await Task.Delay(25);
                waited += 25;
            }
        }
    }
}
