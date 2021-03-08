﻿using System;
using System.Threading.Tasks;
using NLog;

namespace FillInTheTextBot.Services.Extensions
{
    public static class TasksExtensions
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

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
                    Log.Error(e, "Error while executing the task");
                }
            }).ConfigureAwait(false);
        }
    }
}
