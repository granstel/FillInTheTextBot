using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace FillInTheTextBot.Services;

public static class MetricsCollector
{
    public const string MeterName = "FillInTheTextBot.Metrics";
    private static readonly Counter<long> MetricsCounter;

    static MetricsCollector()
    {
        var meter = new Meter(MeterName, "1.0.0");
        MetricsCounter = meter.CreateCounter<long>("metrics", "Custom metrics");
    }

    public static void Increment(string key, string value)
    {
        MetricsCounter.Add(1, new KeyValuePair<string, object?>("metric_name", key), 
                              new KeyValuePair<string, object?>("parameter", value));
    }
}