using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FillInTheTextBot.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FillInTheTextBot.Api.E2ETests.Infrastructure;

[TestFixture]
public abstract class BaseE2ETest
{
	protected TestWebApplicationFactory<Startup> Factory { get; private set; } = null!;
	protected HttpClient Client { get; private set; } = null!;

	[OneTimeSetUp]
	public async Task OneTimeSetUp()
	{
		try
		{
			// Create factory with timeout
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			
			Console.WriteLine("Creating test web application factory...");
			Factory = new TestWebApplicationFactory<Startup>();
			
			Console.WriteLine("Creating HTTP client...");
			Client = Factory.CreateClient();
			
			// Test that the server is responsive
			Console.WriteLine("Testing server responsiveness...");
			var healthCheckTask = Client.GetAsync("/", cts.Token);
			await healthCheckTask; // This will throw if server doesn't start
			
			Console.WriteLine("E2E test setup completed successfully.");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Failed to set up E2E tests: {ex.Message}");
			Console.WriteLine($"Stack trace: {ex.StackTrace}");
			
			// Clean up on failure
			Client?.Dispose();
			Factory?.Dispose();
			
			throw;
		}
	}

	[OneTimeTearDown]
	public void OneTimeTearDown()
	{
		try
		{
			Console.WriteLine("Disposing E2E test resources...");
			Client?.Dispose();
			Factory?.Dispose();
			Console.WriteLine("E2E test cleanup completed.");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error during E2E test cleanup: {ex.Message}");
		}
	}

	protected async Task<HttpResponseMessage> PostJsonAsync<T>(string requestUri, T content)
	{
		var json = JsonConvert.SerializeObject(content);
		var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
		
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		return await Client.PostAsync(requestUri, httpContent, cts.Token);
	}

	protected async Task<HttpResponseMessage> GetAsync(string requestUri)
	{
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		return await Client.GetAsync(requestUri, cts.Token);
	}

	protected async Task<T?> ReadAsAsync<T>(HttpResponseMessage response)
	{
		var content = await response.Content.ReadAsStringAsync();
		return JsonConvert.DeserializeObject<T>(content);
	}

	protected async Task<string> ReadAsStringAsync(HttpResponseMessage response)
	{
		return await response.Content.ReadAsStringAsync();
	}
}