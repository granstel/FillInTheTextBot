using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace FillInTheTextBot.Services.Extensions
{
    public static class TasksExtensions
    {
        private static readonly ILogger Log;

        static TasksExtensions()
        {
            Log = InternalLoggerFactory.CreateLogger(typeof(TaskExtensions).Name);
        }

        /// <summary>
        /// Fire-and-forget
        /// Позволяет не дожидаться завершения задачи.
        /// В случае ошибки исключение будет логировано.
        /// </summary>
        /// <param name="task"></param>
        public static void Forget(this Task task)
        {
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Log?.LogError(e, "Error while executing the task");
                }
            }).ConfigureAwait(false);
        }
    }
}
