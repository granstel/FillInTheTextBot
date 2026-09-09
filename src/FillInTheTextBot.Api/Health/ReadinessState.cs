namespace FillInTheTextBot.Api.Health
{
    /// <summary>
    /// Готовность экземпляра принимать новые запросы. Нужна для бесшовного обновления:
    /// перед остановкой экземпляр объявляет себя неготовым, балансировщик выводит его
    /// из ротации, и только после этого приложение действительно останавливается.
    /// </summary>
    public sealed class ReadinessState
    {
        private volatile bool _isShuttingDown;

        public bool IsReady => !_isShuttingDown;

        public void BeginShutdown()
        {
            _isShuttingDown = true;
        }
    }
}
