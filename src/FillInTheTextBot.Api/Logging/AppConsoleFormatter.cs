using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace FillInTheTextBot.Api.Logging;

internal sealed class AppConsoleFormatter() : ConsoleFormatter(FormatterName)
{
    internal const string FormatterName = "fitb-json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public override void Write<TState>(in LogEntry<TState> entry, IExternalScopeProvider scopes, TextWriter writer)
    {
        // Платформенные сообщения могут содержать URL и другие данные запроса.
        var category = entry.Category;
        var message = category.StartsWith("FillInTheTextBot.", StringComparison.Ordinal)
            || category is "Microsoft.Hosting.Lifetime" or "Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware"
            ? entry.Formatter(entry.State, entry.Exception)
            : $"{category}: {entry.EventId}";
        var logger = category[(category.LastIndexOf('.') + 1)..];
        Write(writer, entry.LogLevel, logger, message, entry.Exception);
    }

    internal static void Write(TextWriter writer, LogLevel severity, string logger, string message, Exception error)
    {
        var level = severity switch
        {
            LogLevel.Trace => "trace",
            LogLevel.Debug => "debug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "error",
            LogLevel.Critical => "fatal",
            _ => throw new ArgumentOutOfRangeException(nameof(severity))
        };

        var exception = error is null ? null : $"{error.GetType().FullName}\n{new StackTrace(error)}";
        writer.WriteLine(JsonSerializer.Serialize(new
        {
            time = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            level,
            logger,
            message,
            exception
        }, JsonOptions));
    }
}
