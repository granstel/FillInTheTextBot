using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Services;

public static class InternalLoggerFactory
{
    public static ILoggerFactory Factory { get; set; }

    public static ILogger<T> CreateLogger<T>()
    {
        return Factory?.CreateLogger<T>();
    }

    public static ILogger CreateLogger(string categoryName)
    {
        return Factory?.CreateLogger(categoryName);
    }
}