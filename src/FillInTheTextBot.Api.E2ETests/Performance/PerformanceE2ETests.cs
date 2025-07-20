using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using FillInTheTextBot.Api.E2ETests.Infrastructure;
using NUnit.Framework;

namespace FillInTheTextBot.Api.E2ETests.Performance;

[TestFixture]
[Category("E2E")]
public class PerformanceE2ETests : BaseE2ETest
{
	[Test]
	public async Task GetInfo_ShouldRespondWithinAcceptableTime()
	{
		// Arrange
		var stopwatch = Stopwatch.StartNew();

		// Act
		var response = await Client.GetAsync("/Yandex");
		stopwatch.Stop();

		// Assert
		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000), 
			"GET request should complete within 5 seconds");
	}

	[Test]
	public async Task WebHook_ShouldProcessRequestWithinAcceptableTime()
	{
		// Arrange
		var input = new
		{
			request = new
			{
				command = "test command",
				original_utterance = "test command",
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

		var stopwatch = Stopwatch.StartNew();

		// Act
		var response = await PostJsonAsync("/Yandex", input);
		stopwatch.Stop();

		// Assert
		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(10000), 
			"POST request should complete within 10 seconds");
	}

	[Test]
	public async Task Application_ShouldHandleMultipleSimultaneousRequests()
	{
		// Arrange
		const int numberOfRequests = 20;
		var tasks = new Task<HttpResponseMessage>[numberOfRequests];
		var stopwatch = Stopwatch.StartNew();

		// Act
		for (int i = 0; i < numberOfRequests; i++)
		{
			int index = i; // Capture the loop variable
			tasks[index] = Client.GetAsync("/Yandex");
		}

		var responses = await Task.WhenAll(tasks);
		stopwatch.Stop();

		// Assert
		Assert.That(responses.Length, Is.EqualTo(numberOfRequests));
		
		foreach (var response in responses)
		{
			Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
			response.Dispose();
		}

		Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(15000), 
			"All concurrent requests should complete within 15 seconds");
	}

	[Test]
	public async Task Application_ShouldHandleResourceIntensiveOperations()
	{
		// Arrange
		var largeInput = new
		{
			request = new
			{
				command = "Tell me a very long story about " + new string('x', 1000),
				original_utterance = "Tell me a story",
				type = "SimpleUtterance"
			},
			session = new
			{
				session_id = "performance-test-session",
				message_id = 1,
				user_id = "performance-test-user",
				@new = true
			},
			version = "1.0"
		};

		var stopwatch = Stopwatch.StartNew();

		// Act
		var response = await PostJsonAsync("/Yandex", largeInput);
		stopwatch.Stop();

		// Assert
		Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError));
		Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(30000), 
			"Resource intensive request should complete within 30 seconds");
	}
}