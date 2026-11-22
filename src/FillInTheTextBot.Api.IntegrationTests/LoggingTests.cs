using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace FillInTheTextBot.Api.IntegrationTests;

public class LoggingTests
{
    [Test]
    public async Task ConsoleFormatterPreservesCollectorFieldsAndOmitsRequestData()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AppConfiguration:Rotation:Enabled", "false");
            builder.UseSetting("AppConfiguration:Tracing:Enabled", "false");
        });
        var options = factory.Services.GetRequiredService<IOptions<ConsoleLoggerOptions>>().Value;
        var formatter = factory.Services.GetServices<ConsoleFormatter>().Single(item => item.Name == options.FormatterName);
        using var output = new StringWriter();
        var entry = new LogEntry<string>(
            LogLevel.Critical, "Microsoft.AspNetCore.Server.Kestrel", new EventId(13, "ApplicationError"),
            "synthetic-path-token", new InvalidOperationException("synthetic-session-id\nsynthetic-request-body"),
            (state, error) => $"{state}: {error}");

        formatter.Write(entry, new LoggerExternalScopeProvider(), output);

        var line = output.ToString();
        using var record = JsonDocument.Parse(line);
        var fields = record.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(line.Split('\n', StringSplitOptions.RemoveEmptyEntries), Has.Length.EqualTo(1));
            Assert.That(line, Does.Not.Contain("synthetic-"));
            Assert.That(fields.GetProperty("level").GetString(), Is.EqualTo("fatal"));
            Assert.That(fields.GetProperty("logger").GetString(), Is.EqualTo("Kestrel"));
            Assert.That(fields.GetProperty("message").GetString(), Does.Contain("ApplicationError"));
            Assert.That(fields.GetProperty("exception").GetString(), Does.Contain("InvalidOperationException"));
            Assert.That(DateTimeOffset.TryParseExact(fields.GetProperty("time").GetString(),
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out _), Is.True);
        });
    }
}
