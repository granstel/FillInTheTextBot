using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Api.E2ETests.Infrastructure;

public class TestWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup>
	where TStartup : class
{
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.ConfigureAppConfiguration((context, config) =>
		{
			config.AddJsonFile("appsettings.Test.json", optional: false, reloadOnChange: true);
		});

		builder.ConfigureServices(services =>
		{
			// Override services for testing if needed
			// For example, replace external dependencies with mocks
			
			// Configure logging for tests
			services.AddLogging(logging =>
			{
				logging.ClearProviders();
				logging.AddConsole();
				logging.SetMinimumLevel(LogLevel.Warning);
			});
		});

		builder.UseEnvironment("Test");
		
		// Use a dynamic port to avoid conflicts
		builder.UseUrls();
	}
}