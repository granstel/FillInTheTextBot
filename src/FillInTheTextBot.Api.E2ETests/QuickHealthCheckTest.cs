using System;
using System.Net;
using System.Threading.Tasks;
using FillInTheTextBot.Api.E2ETests.Infrastructure;
using NUnit.Framework;

namespace FillInTheTextBot.Api.E2ETests;

[TestFixture]
[Category("E2E")]
[Category("Health")]
public class QuickHealthCheckTest : BaseE2ETest
{
	[Test]
	[CancelAfter(15000)] // 15 second timeout
	public async Task Server_ShouldStart_WithoutHanging()
	{
		// Arrange & Act
		Console.WriteLine("Starting quick health check...");
		
		try
		{
			var response = await GetAsync("/");
			Console.WriteLine($"Received response with status: {response.StatusCode}");
			
			// Assert - we just want to ensure the server responds (any response is fine)
			Assert.That(response, Is.Not.Null, "Response should not be null");
			
			// Log the result for debugging
			var content = await ReadAsStringAsync(response);
			Console.WriteLine($"Response content length: {content?.Length ?? 0}");
			
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Health check failed: {ex.Message}");
			throw;
		}
		
		Console.WriteLine("Quick health check completed successfully.");
	}
	
	[Test]
	[CancelAfter(15000)] // 15 second timeout
	public async Task Server_ShouldRespond_ToNonExistentRoute()
	{
		// Arrange & Act
		Console.WriteLine("Testing non-existent route...");
		
		var response = await GetAsync("/non-existent-route");
		
		// Assert - Should get 404 or similar (not hang)
		Assert.That(response, Is.Not.Null);
		Console.WriteLine($"Non-existent route returned: {response.StatusCode}");
		
		// Common status codes for non-existent routes
		Assert.That(
			response.StatusCode, 
			Is.AnyOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed, HttpStatusCode.BadRequest),
			"Should return a client error status code for non-existent route"
		);
	}
}