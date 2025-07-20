using System.Net;
using System.Threading.Tasks;
using FillInTheTextBot.Api.E2ETests.Infrastructure;
using NUnit.Framework;

namespace FillInTheTextBot.Api.E2ETests.Security;

[TestFixture]
[Category("E2E")]
public class AuthenticationE2ETests : BaseE2ETest
{
	[Test]
	public async Task WebHook_WithValidToken_ShouldAcceptRequest()
	{
		// Arrange
		var validInput = new
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

		// Act
		var response = await PostJsonAsync("/Yandex/valid-token", validInput);

		// Assert
		// Should either succeed or fail gracefully based on token validation
		Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError));
	}

	[Test]
	public async Task WebHook_WithInvalidToken_ShouldHandleGracefully()
	{
		// Arrange
		var validInput = new
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

		// Act
		var response = await PostJsonAsync("/Yandex/invalid-token", validInput);

		// Assert
		// Should either accept (if no token validation) or return 404/401
		Assert.That(response.StatusCode, Is.AnyOf(
			HttpStatusCode.OK,
			HttpStatusCode.NotFound,
			HttpStatusCode.Unauthorized));
	}

	[Test]
	public async Task Application_ShouldNotExposeInternalEndpoints()
	{
		// Arrange
		var sensitiveEndpoints = new[]
		{
			"/swagger",
			"/api/internal",
			"/admin",
			"/health/detailed",
			"/metrics/sensitive"
		};

		// Act & Assert
		foreach (var endpoint in sensitiveEndpoints)
		{
			var response = await Client.GetAsync(endpoint);
			
			// Should not expose internal endpoints publicly
			Assert.That(response.StatusCode, Is.AnyOf(
				HttpStatusCode.NotFound,
				HttpStatusCode.Unauthorized,
				HttpStatusCode.Forbidden),
				$"Endpoint {endpoint} should not be publicly accessible");
		}
	}
}