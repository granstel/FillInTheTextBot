namespace FillInTheTextBot.Services.Configuration
{
    public class ShutdownConfiguration
    {
        /// <summary>
        /// Сколько секунд экземпляр держится поднятым после объявления себя неготовым,
        /// чтобы балансировщик успел увести на него трафик. Должно быть заметно больше
        /// интервала проверки здоровья у балансировщика.
        /// </summary>
        public int DrainDelaySeconds { get; set; } = 10;

        /// <summary>
        /// Сколько секунд хост ждёт завершения текущих запросов и разбора очереди
        /// фоновых работ после паузы вывода из ротации.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
    }
}
