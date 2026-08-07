using System;
using System.Threading.Tasks;

namespace FillInTheTextBot.Services.BackgroundTasks
{
    /// <summary>
    /// Очередь работ, которые не нужно дожидаться в рамках обработки запроса
    /// (запись в кэш, установка контекста Dialogflow).
    /// </summary>
    public interface IBackgroundTaskQueue
    {
        /// <summary>
        /// Ставит работу в очередь. Не блокирует вызывающий поток: если очередь переполнена,
        /// работа отбрасывается — задержать ответ пользователю хуже, чем потерять запись.
        /// </summary>
        /// <param name="name">Имя работы, попадает в лог при ошибке или отбрасывании.</param>
        /// <param name="work">Работа.</param>
        /// <returns>true, если работа принята в очередь.</returns>
        bool Enqueue(string name, Func<Task> work);
    }
}
