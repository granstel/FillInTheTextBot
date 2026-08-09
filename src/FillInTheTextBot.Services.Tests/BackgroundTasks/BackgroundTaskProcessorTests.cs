using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FillInTheTextBot.Services.BackgroundTasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

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


            Assert.That(accepted, Is.True);
            Assert.That(completed, Is.SameAs(executed.Task), "Работа из очереди должна быть выполнена");
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


            Assert.That(executed.Count, Is.EqualTo(count));
            Assert.That(executed, Is.EquivalentTo(Enumerable.Range(0, count)));
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


            Assert.That(completed, Is.SameAs(executed.Task), "Исключение в одной работе не должно останавливать обработчик");
        }

        [Test]
        public async Task Enqueue_WorkBehindSlowOne_NotBlocked()
        {
            // Прежний fire-and-forget выполнял работы одновременно, и медленная не
            // задерживала остальные. Это свойство должно сохраняться
            var slowStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var slowFinish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var fastExecuted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            await _target.StartAsync(CancellationToken.None);

            _queue.Enqueue("медленная", () =>
            {
                slowStarted.TrySetResult(true);
                return slowFinish.Task;
            });

            await slowStarted.Task;

            _queue.Enqueue("быстрая", () =>
            {
                fastExecuted.TrySetResult(true);
                return Task.CompletedTask;
            });


            var completed = await Task.WhenAny(fastExecuted.Task, Task.Delay(5000));


            slowFinish.TrySetResult(true);

            Assert.That(completed, Is.SameAs(fastExecuted.Task),
                "Быстрая работа не должна ждать завершения медленной");
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

            Assert.That(completed, Is.SameAs(executed.Task), "Принятые работы должны успеть выполниться при остановке");
        }

        [Test]
        public async Task StopAsync_InFlightWork_WaitsUntilCompleted()
        {
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            await _target.StartAsync(CancellationToken.None);

            _queue.Enqueue("в полёте", async () =>
            {
                started.TrySetResult(true);
                await release.Task;
                finished.TrySetResult(true);
            });

            await started.Task; // работа стартовала и висит на release

            var stop = _target.StopAsync(CancellationToken.None);

            // Пока работа не отпущена, StopAsync не должен завершиться
            var early = await Task.WhenAny(stop, Task.Delay(300));
            Assert.That(early, Is.Not.SameAs(stop), "StopAsync не должен завершаться, пока работа в полёте");
            Assert.That(finished.Task.IsCompleted, Is.False);

            release.TrySetResult(true);

            await stop; // после отпускания StopAsync завершается

            Assert.That(finished.Task.IsCompletedSuccessfully, Is.True,
                "Работа должна быть доведена до конца до завершения StopAsync");
        }

        [Test]
        public void Enqueue_QueueIsFull_TaskDropped()
        {
            // Обработчик не запущен, поэтому очередь только наполняется
            foreach (var i in Enumerable.Range(0, BackgroundTaskQueue.Capacity))
            {
                var accepted = _queue.Enqueue($"работа-{i}", () => Task.CompletedTask);

                Assert.That(accepted, Is.True, $"Работа {i} должна помещаться в очередь");
            }


            var overflow = _queue.Enqueue("лишняя", () => Task.CompletedTask);


            Assert.That(overflow, Is.False, "Переполнение очереди не должно блокировать вызывающий поток");
        }

        [Test]
        public void Enqueue_NullWork_NotAccepted()
        {
            var accepted = _queue.Enqueue("пустая", null);

            Assert.That(accepted, Is.False);
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
