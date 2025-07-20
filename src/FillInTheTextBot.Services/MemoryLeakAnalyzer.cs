using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Services;

/// <summary>
/// Анализатор потенциальных утечек памяти
/// </summary>
public static class MemoryLeakAnalyzer
{
    private static readonly ILogger Log = InternalLoggerFactory.CreateLogger(nameof(MemoryLeakAnalyzer));

    /// <summary>
    /// Анализирует использование памяти приложением и выводит детальную информацию
    /// </summary>
    public static void AnalyzeMemoryUsage(string context = "")
    {
        try
        {
            var process = Process.GetCurrentProcess();
            
            var analysis = new
            {
                Context = context,
                Timestamp = DateTime.UtcNow,
                
                // Managed heap
                ManagedMemory = new
                {
                    TotalAllocated = GC.GetTotalMemory(false),
                    TotalAfterGC = GC.GetTotalMemory(true),
                    Generation0Collections = GC.CollectionCount(0),
                    Generation1Collections = GC.CollectionCount(1),
                    Generation2Collections = GC.CollectionCount(2),
                    IsServerGC = GCSettings.IsServerGC,
                    LatencyMode = GCSettings.LatencyMode.ToString()
                },
                
                // Process memory
                ProcessMemory = new
                {
                    WorkingSet = process.WorkingSet64,
                    PrivateMemorySize = process.PrivateMemorySize64,
                    VirtualMemorySize = process.VirtualMemorySize64,
                    PagedMemorySize = process.PagedMemorySize64,
                    NonpagedSystemMemorySize = process.NonpagedSystemMemorySize64
                },
                
                // Thread information
                ThreadInfo = new
                {
                    ThreadPoolWorkerThreads = GetThreadPoolInfo().workerThreads,
                    ThreadPoolCompletionPortThreads = GetThreadPoolInfo().completionPortThreads,
                    TotalThreads = process.Threads.Count,
                    AppDomains = GetAppDomainCount()
                },
                
                // Assembly information
                LoadedAssemblies = AppDomain.CurrentDomain.GetAssemblies().Length
            };

            Log?.LogWarning("=== MEMORY ANALYSIS ({Context}) ===\n" +
                           "Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss} UTC\n" +
                           "\n📊 MANAGED HEAP:\n" +
                           "  • Total Allocated: {ManagedTotalAllocated:N0} bytes ({ManagedTotalAllocatedMB:F2} MB)\n" +
                           "  • After GC: {ManagedTotalAfterGC:N0} bytes ({ManagedTotalAfterGCMB:F2} MB)\n" +
                           "  • Potentially reclaimable: {PotentiallyReclaimable:N0} bytes ({PotentiallyReclaimableMB:F2} MB)\n" +
                           "  • Gen0 collections: {Gen0Collections}\n" +
                           "  • Gen1 collections: {Gen1Collections}\n" +
                           "  • Gen2 collections: {Gen2Collections}\n" +
                           "  • Server GC: {IsServerGC}\n" +
                           "  • Latency Mode: {LatencyMode}\n" +
                           "\n💻 PROCESS MEMORY:\n" +
                           "  • Working Set: {WorkingSet:N0} bytes ({WorkingSetMB:F2} MB)\n" +
                           "  • Private Memory: {PrivateMemorySize:N0} bytes ({PrivateMemorySizeMB:F2} MB)\n" +
                           "  • Virtual Memory: {VirtualMemorySize:N0} bytes ({VirtualMemorySizeMB:F2} MB)\n" +
                           "  • Paged Memory: {PagedMemorySize:N0} bytes ({PagedMemorySizeMB:F2} MB)\n" +
                           "\n🧵 THREADS:\n" +
                           "  • ThreadPool Worker Threads: {ThreadPoolWorkerThreads}\n" +
                           "  • ThreadPool I/O Threads: {ThreadPoolCompletionPortThreads}\n" +
                           "  • Total Process Threads: {TotalThreads}\n" +
                           "\n📚 LOADED RESOURCES:\n" +
                           "  • Loaded Assemblies: {LoadedAssemblies}\n" +
                           "  • App Domains: {AppDomains}",
                
                analysis.Context,
                analysis.Timestamp,
                
                analysis.ManagedMemory.TotalAllocated,
                analysis.ManagedMemory.TotalAllocated / 1024.0 / 1024.0,
                analysis.ManagedMemory.TotalAfterGC,
                analysis.ManagedMemory.TotalAfterGC / 1024.0 / 1024.0,
                analysis.ManagedMemory.TotalAllocated - analysis.ManagedMemory.TotalAfterGC,
                (analysis.ManagedMemory.TotalAllocated - analysis.ManagedMemory.TotalAfterGC) / 1024.0 / 1024.0,
                analysis.ManagedMemory.Generation0Collections,
                analysis.ManagedMemory.Generation1Collections,
                analysis.ManagedMemory.Generation2Collections,
                analysis.ManagedMemory.IsServerGC,
                analysis.ManagedMemory.LatencyMode,
                
                analysis.ProcessMemory.WorkingSet,
                analysis.ProcessMemory.WorkingSet / 1024.0 / 1024.0,
                analysis.ProcessMemory.PrivateMemorySize,
                analysis.ProcessMemory.PrivateMemorySize / 1024.0 / 1024.0,
                analysis.ProcessMemory.VirtualMemorySize,
                analysis.ProcessMemory.VirtualMemorySize / 1024.0 / 1024.0,
                analysis.ProcessMemory.PagedMemorySize,
                analysis.ProcessMemory.PagedMemorySize / 1024.0 / 1024.0,
                
                analysis.ThreadInfo.ThreadPoolWorkerThreads,
                analysis.ThreadInfo.ThreadPoolCompletionPortThreads,
                analysis.ThreadInfo.TotalThreads,
                
                analysis.LoadedAssemblies,
                analysis.ThreadInfo.AppDomains
            );

            // Дополнительные предупреждения
            CheckForPotentialLeaks(analysis);
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "Error during memory analysis");
        }
    }

    private static void CheckForPotentialLeaks(dynamic analysis)
    {
        var warnings = new List<string>();

        // Проверка на высокое использование памяти
        double managedMemoryMB = analysis.ManagedMemory.TotalAllocated / 1024.0 / 1024.0;
        if (managedMemoryMB > 500) // 500 MB
        {
            warnings.Add($"⚠️  High managed memory usage: {managedMemoryMB:F2} MB");
        }

        // Проверка на низкую эффективность GC
        if (analysis.ManagedMemory.Generation2Collections > 100)
        {
            warnings.Add($"⚠️  High Gen2 GC collections: {analysis.ManagedMemory.Generation2Collections} (potential memory pressure)");
        }

        // Проверка на много потоков
        if (analysis.ThreadInfo.TotalThreads > 50)
        {
            warnings.Add($"⚠️  High thread count: {analysis.ThreadInfo.TotalThreads} threads");
        }

        // Проверка на много загруженных сборок
        if (analysis.LoadedAssemblies > 200)
        {
            warnings.Add($"⚠️  High number of loaded assemblies: {analysis.LoadedAssemblies}");
        }

        // Проверка на большой разрыв между выделенной и освобожденной памятью
        long potentiallyReclaimable = analysis.ManagedMemory.TotalAllocated - analysis.ManagedMemory.TotalAfterGC;
        if (potentiallyReclaimable > 100 * 1024 * 1024) // 100 MB
        {
            warnings.Add($"⚠️  Large amount of potentially reclaimable memory: {potentiallyReclaimable / 1024.0 / 1024.0:F2} MB");
        }

        if (warnings.Any())
        {
            Log?.LogWarning("🚨 POTENTIAL MEMORY ISSUES DETECTED:\n{Warnings}",
                string.Join("\n", warnings));
        }
        else
        {
            Log?.LogInformation("✅ Memory usage appears healthy");
        }
    }

    private static (int workerThreads, int completionPortThreads) GetThreadPoolInfo()
    {
        ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
        ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);
        return (maxWorkerThreads - workerThreads, maxCompletionPortThreads - completionPortThreads);
    }

    private static int GetAppDomainCount()
    {
        try
        {
            // В .NET Core всегда один AppDomain, но оставим для совместимости
            return 1;
        }
        catch
        {
            return 1;
        }
    }

    /// <summary>
    /// Принудительно вызывает полную сборку мусора и анализирует результат
    /// </summary>
    public static void ForceGCAndAnalyze(string context = "")
    {
        try
        {
            Log?.LogInformation("🗑️  Starting forced garbage collection for analysis...");
            
            var beforeGC = GC.GetTotalMemory(false);
            var gen0Before = GC.CollectionCount(0);
            var gen1Before = GC.CollectionCount(1);
            var gen2Before = GC.CollectionCount(2);
            
            var stopwatch = Stopwatch.StartNew();
            
            GC.Collect(2, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced);
            
            stopwatch.Stop();
            
            var afterGC = GC.GetTotalMemory(true);
            var gen0After = GC.CollectionCount(0);
            var gen1After = GC.CollectionCount(1);
            var gen2After = GC.CollectionCount(2);
            
            var memoryFreed = beforeGC - afterGC;
            
            Log?.LogInformation("🗑️  Garbage collection completed in {ElapsedMs} ms:\n" +
                               "  • Memory before GC: {BeforeGC:N0} bytes ({BeforeGCMB:F2} MB)\n" +
                               "  • Memory after GC: {AfterGC:N0} bytes ({AfterGCMB:F2} MB)\n" +
                               "  • Memory freed: {MemoryFreed:N0} bytes ({MemoryFreedMB:F2} MB)\n" +
                               "  • GC cycles: Gen0={Gen0Delta}, Gen1={Gen1Delta}, Gen2={Gen2Delta}",
                
                stopwatch.ElapsedMilliseconds,
                beforeGC, beforeGC / 1024.0 / 1024.0,
                afterGC, afterGC / 1024.0 / 1024.0,
                memoryFreed, memoryFreed / 1024.0 / 1024.0,
                gen0After - gen0Before,
                gen1After - gen1Before,
                gen2After - gen2Before
            );

            // Выполняем анализ после GC
            AnalyzeMemoryUsage($"{context} (after GC)");
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "Error during forced GC and analysis");
        }
    }
}