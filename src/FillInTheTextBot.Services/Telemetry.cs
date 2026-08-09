namespace FillInTheTextBot.Services
{
    /// <summary>
    /// Общие константы телеметрии. Единое имя, под которым приложение публикует
    /// активности (ActivitySource) и метрики (Meter) — в OpenTelemetry это
    /// instrumentation scope (otel_scope_name).
    /// </summary>
    public static class Telemetry
    {
        public const string ScopeName = "FillInTheTextBot";
    }
}
