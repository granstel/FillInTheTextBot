using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FillInTheTextBot.Api.Health
{
    public sealed class ReadinessHealthCheck : IHealthCheck
    {
        private readonly ReadinessState _state;

        public ReadinessHealthCheck(ReadinessState state)
        {
            _state = state;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var result = _state.IsReady
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Экземпляр останавливается");

            return Task.FromResult(result);
        }
    }
}
