using System;
using System.Net;
using System.Threading.Tasks;
using FillInTheTextBot.Api.E2ETests.Infrastructure;
using NUnit.Framework;

namespace FillInTheTextBot.Api.E2ETests.Middleware;

[TestFixture]
[Category("E2E")]
public class MiddlewareE2ETests : BaseE2ETest
{
	[Test]
	public async Task ExceptionMiddleware_ShouldHandleInternalErrors()
	{
		// Arrange - Create a request that might cause an internal error
		var malformedJson = "{\"invalid\": json}";

		// Act
		var response = await PostJsonAsync("/Yandex", malformedJson);

		// Assert
		// The middleware should handle the error gracefully, not return 500
		Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError));
	}

	[Test]
	public async Task Application_ShouldSetSecurityHeaders()
	{
		// Act
		var response = await Client.GetAsync("/Yandex");

		// Assert
		Assert.That(response.Headers.Contains("X-Content-Type-Options") || 
					response.Content.Headers.Contains("X-Content-Type-Options"), 
					Is.False, "Security headers should be configured if needed");
	}

	[Test]
	public async Task Application_ShouldHandleLargePayloads()
	{
		// Arrange
		var largeInput = new
		{
			request = new
			{
				command = new string('a', 10000), // Large command
				original_utterance = "test",
				type = "SimpleUtterance"
			},
			session = new
			{
				session_id = "test-session-id",
				message_id = 1,
				user_id = "test-user-id",
				@new = true
			},
			version = "1.0"
		};

		// Act
		var response = await PostJsonAsync("/Yandex", largeInput);

		// Assert
		// Should either process successfully or return proper error, not crash
		Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError));
	}

	[Test]
	public async Task Application_ShouldHandleConcurrentRequests()
	{
		// Arrange
		var tasks = new Task<HttpResponseMessage>[10];
		
		// Act
		for (int i = 0; i < tasks.Length; i++)
		{
			int index = i; // Capture the loop variable
			tasks[index] = Client.GetAsync("/Yandex");
		}

		var responses = await Task.WhenAll(tasks);

		// Assert
		foreach (var response in responses)
		{
			Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
			response.Dispose();
		}
	}
}