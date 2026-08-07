using System;
using System.Threading;
using System.Threading.Tasks;
using FillInTheTextBot.Services.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Api.Health
{
    /// <summary>
    /// Даёт балансировщику время вывести экземпляр из ротации до фактической остановки.
    ///
    /// Без этой паузы порядок такой: приложение перестаёт слушать порт, и только потом
    /// балансировщик замечает, что проверка здоровья не проходит — запросы, попавшие
    /// в этот промежуток, теряются. С паузой сначала краснеет проверка здоровья,
    /// балансировщик уводит трафик, и лишь затем закрывается порт.
    /// </summary>
    public sealed class GracefulShutdownService : IHostedService
    {
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ReadinessState _state;
        private readonly ShutdownConfiguration _configuration;
        private readonly ILogger<GracefulShutdownService> _log;

        public GracefulShutdownService(
            IHostApplicationLifetime lifetime,
            ReadinessState state,
            ShutdownConfiguration configuration,
            ILogger<GracefulShutdownService> log)
        {
            _lifetime = lifetime;
            _state = state;
            _configuration = configuration;
            _log = log;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _lifetime.ApplicationStopping.Register(OnStopping);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private void OnStopping()
        {
            _state.BeginShutdown();

            var delay = TimeSpan.FromSeconds(_configuration.DrainDelaySeconds);

            if (delay <= TimeSpan.Zero)
            {
                return;
            }

            _log.LogInformation("Instance is marked as not ready, draining traffic for {Delay}", delay);

            // Обработчик ApplicationStopping синхронный: хост дожидается его завершения,
            // и это ровно то, что нужно — пауза удерживает приложение поднятым
            Thread.Sleep(delay);
        }
    }
}
