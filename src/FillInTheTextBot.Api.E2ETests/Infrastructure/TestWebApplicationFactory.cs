using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Collections.Generic;

namespace FillInTheTextBot.Api.E2ETests.Infrastructure;

public class TestWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup>
	where TStartup : class
{
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.ConfigureAppConfiguration((context, config) =>
		{
			// Clear existing configuration sources to avoid conflicts
			config.Sources.Clear();
			
			// Add test-specific configuration
			config.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Logging:LogLevel:Default"] = "Warning",
				["Logging:LogLevel:Microsoft"] = "Warning",
				["Logging:LogLevel:System"] = "Warning",
				["AllowedHosts"] = "*",
				// Disable external dependencies for tests
				["OpenTelemetry:Enabled"] = "false",
				["HttpLogging:Enabled"] = "false"
			});
			
			// Try to add test config file if it exists
			var testConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Test.json");
			if (File.Exists(testConfigPath))
			{
				config.AddJsonFile("appsettings.Test.json", optional: true);
			}
		});

		builder.ConfigureServices(services =>
		{
			// Override potentially problematic services for testing
			
			// Configure minimal logging for tests
			services.AddLogging(logging =>
			{
				logging.ClearProviders();
				logging.AddConsole();
				logging.SetMinimumLevel(LogLevel.Error); // Only show errors in tests
			});
			
			// Remove OpenTelemetry for tests to avoid external dependencies
			var telemetryDescriptor = services.FirstOrDefault(d => d.ServiceType.Name.Contains("OpenTelemetry"));
			if (telemetryDescriptor != null)
			{
				services.Remove(telemetryDescriptor);
			}
		});

		builder.UseEnvironment("Test");
		
		// TestServer will be used automatically by WebApplicationFactory
		
		// Suppress startup messages to reduce noise
		builder.CaptureStartupErrors(true);
		builder.UseSetting("SuppressStatusMessages", "true");
	}
	
	protected override IHostBuilder? CreateHostBuilder()
	{
		// Return null to use the existing WebHost configuration
		return null;
	}
}