using Prometheus;

namespace FillInTheTextBot.Services;

public static class MetricsCollector
{
    private static readonly Gauge _statistics;

    static MetricsCollector()
    {
        _statistics = Metrics
            .CreateGauge("statistics", "Statistics", "statistic_name", "parameter");
    }

    public static void Increment(string key, string value)
    {
        _statistics.WithLabels(key, value).Inc();
    }
}