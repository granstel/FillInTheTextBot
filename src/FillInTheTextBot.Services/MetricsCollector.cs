using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace FillInTheTextBot.Services;

public static class MetricsCollector
{
    public const string MeterName = "FillInTheTextBot.Metrics";
    private static readonly Meter Meter;
    private static readonly Counter<long> MetricsCounter;

    static MetricsCollector()
    {
        Meter = new Meter(MeterName, "1.0.0");
        MetricsCounter = Meter.CreateCounter<long>("metrics", "Custom metrics");
        
        // Обеспечиваем освобождение ресурсов при завершении приложения
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Meter?.Dispose();
        AppDomain.CurrentDomain.DomainUnload += (_, _) => Meter?.Dispose();
    }

    public static void Increment(string key, string value)
    {
        MetricsCounter.Add(1, new KeyValuePair<string, object?>("metric_name", key), 
                              new KeyValuePair<string, object?>("parameter", value));
    }
}