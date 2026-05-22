using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Services;

/// <summary>
/// Класс для диагностики утечек памяти
/// </summary>
public static class MemoryDiagnostics
{
    private static readonly ILogger Log = InternalLoggerFactory.CreateLogger(nameof(MemoryDiagnostics));
    private static long _initialMemoryUsage;
    private static bool _initialized;

    /// <summary>
    /// Инициализирует диагностику памяти
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        
        _initialMemoryUsage = GC.GetTotalMemory(true);
        _initialized = true;
        
        Log?.LogInformation("Memory diagnostics initialized. Initial memory usage: {MemoryUsage} bytes", _initialMemoryUsage);
    }

    /// <summary>
    /// Логирует текущее использование памяти
    /// </summary>
    public static void LogMemoryUsage(string operationName = "")
    {
        try
        {
            var currentMemory = GC.GetTotalMemory(false);
            var memoryDifference = currentMemory - _initialMemoryUsage;
            
            Log?.LogInformation("Memory usage for {OperationName}: Current={CurrentMemory} bytes, " +
                               "Difference from start={MemoryDifference} bytes, " +
                               "Gen0 collections={Gen0}, Gen1 collections={Gen1}, Gen2 collections={Gen2}",
                operationName, currentMemory, memoryDifference,
                GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "Error while logging memory usage");
        }
    }

    /// <summary>
    /// Принудительно вызывает сборку мусора и логирует результат
    /// </summary>
    public static void ForceGarbageCollectionAndLog(string operationName = "")
    {
        try
        {
            var beforeGC = GC.GetTotalMemory(false);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var afterGC = GC.GetTotalMemory(true);
            var freed = beforeGC - afterGC;
            
            Log?.LogInformation("Garbage collection completed for {OperationName}: " +
                               "Before={BeforeGC} bytes, After={AfterGC} bytes, Freed={FreedMemory} bytes",
                operationName, beforeGC, afterGC, freed);
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "Error during garbage collection");
        }
    }

    /// <summary>
    /// Периодически логирует использование памяти (для background мониторинга)
    /// </summary>
    public static void StartPeriodicMemoryLogging(TimeSpan interval)
    {
        Task.Run(async () =>
        {
            var fullAnalysisCounter = 0;
            
            while (true)
            {
                try
                {
                    await Task.Delay(interval).ConfigureAwait(false);
                    LogMemoryUsage("Periodic monitoring");
                    
                    // Каждые 30 минут (6 циклов по 5 минут) выполняем полный анализ
                    fullAnalysisCounter++;
                    if (fullAnalysisCounter >= 6)
                    {
                        fullAnalysisCounter = 0;
                        MemoryLeakAnalyzer.AnalyzeMemoryUsage("Periodic deep analysis");
                    }
                }
                catch (Exception ex)
                {
                    Log?.LogError(ex, "Error in periodic memory logging");
                    break;
                }
            }
        });
    }
}