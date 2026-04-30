using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Services.Extensions;

public static class TasksExtensions
{
    private static readonly ILogger Log;

    static TasksExtensions()
    {
        Log = InternalLoggerFactory.CreateLogger(typeof(TaskExtensions).Name);
    }

    /// <summary>
    ///     Fire-and-forget
    ///     Позволяет не дожидаться завершения задачи.
    ///     В случае ошибки исключение будет логировано.
    /// </summary>
    /// <param name="task"></param>
    public static void Forget(this Task task)
    {
        // Используем Task.Run вместо Task.Factory.StartNew для лучшего управления памятью
        Task.Run(async () =>
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log?.LogError(e, "Error while executing the task");
            }
        });
    }
}