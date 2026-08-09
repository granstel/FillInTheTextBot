using System.Collections.Generic;

namespace FillInTheTextBot.Services.BackgroundTasks
{
    /// <summary>
    /// Потребительская сторона очереди фоновых работ: чтение принятых работ и закрытие
    /// очереди на запись при остановке. Отделена от <see cref="IBackgroundTaskQueue"/>,
    /// чтобы обработчик зависел от абстракции, а не от конкретного класса очереди.
    /// </summary>
    public interface IBackgroundTaskReader
    {
        /// <summary>
        /// Читает принятые работы, пока очередь не закрыта на запись и не разобрана.
        /// </summary>
        IAsyncEnumerable<BackgroundTask> ReadAllAsync();

        /// <summary>
        /// Закрывает очередь на запись. Уже принятые работы остаются доступны для чтения.
        /// </summary>
        void Complete();
    }
}
