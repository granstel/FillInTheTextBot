using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace FillInTheTextBot.Services
{
    public static class MetricsCollector
    {
        /// <summary>
        /// Имя счётчика. Его нужно передать в AddMeter при настройке OpenTelemetry.
        /// </summary>
        public const string MeterName = Telemetry.ScopeName;

        private const string MetricName = "metrics";

        private const string MetricNameLabel = "metric_name";
        private const string ParameterLabel = "parameter";

        private static readonly Meter Meter;

        /// <summary>
        /// Значения по комбинациям меток. Хранятся в памяти, потому что метрика отдаётся
        /// как gauge — см. комментарий ниже.
        /// </summary>
        private static readonly ConcurrentDictionary<(string Key, string Value), long> Values = new();

        static MetricsCollector()
        {
            Meter = new Meter(MeterName);

            // Раньше метрика собиралась prometheus-net как Gauge с именем "metrics" и метками
            // metric_name/parameter, на которую опираются существующие дашборды и алерты.
            // Counter в OpenTelemetry экспортировался бы как "metrics_total", поэтому здесь
            // ObservableGauge: он отдаёт то же имя и те же метки. Смысл у значения при этом
            // счётчиковый — только растёт. Переименование в честный counter сломает дашборды,
            // поэтому делать его нужно отдельно и осознанно.
            Meter.CreateObservableGauge(MetricName, GetMeasurements, description: "Custom metrics");
        }

        public static void Increment(string key, string value)
        {
            Values.AddOrUpdate((key, value), 1, (_, current) => current + 1);
        }

        private static IEnumerable<Measurement<long>> GetMeasurements()
        {
            foreach (var pair in Values)
            {
                yield return new Measurement<long>(
                    pair.Value,
                    new KeyValuePair<string, object>(MetricNameLabel, pair.Key.Key),
                    new KeyValuePair<string, object>(ParameterLabel, pair.Key.Value));
            }
        }
    }
}
