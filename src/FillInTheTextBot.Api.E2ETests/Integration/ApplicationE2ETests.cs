using System.Net;
using System.Threading.Tasks;
using FillInTheTextBot.Api.E2ETests.Infrastructure;
using NUnit.Framework;

namespace FillInTheTextBot.Api.E2ETests.Integration;

[TestFixture]
[Category("E2E")]
public class ApplicationE2ETests : BaseE2ETest
{
	[Test]
	public async Task Application_ShouldStartAndBeHealthy()
	{
		// Act - Try to access any endpoint to verify the application is running
		var response = await Client.GetAsync("/Yandex");

		// Assert
		Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError));
		Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.ServiceUnavailable));
	}

	[Test]
	public async Task AllControllers_ShouldBeAccessible()
	{
		// Arrange
		var controllers = new[] { "/Yandex", "/Sber", "/Marusia" };

		// Act & Assert
		foreach (var controller in controllers)
		{
			var response = await Client.GetAsync(controller);
			
			Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), 
				$"Controller {controller} should be accessible");
			
			var content = await ReadAsStringAsync(response);
			Assert.That(content, Is.Not.Empty, 
				$"Controller {controller} should return non-empty response");
		}
	}

	[Test]
	public async Task Application_ShouldHandleNonExistentRoutes()
	{
		// Act
		var response = await Client.GetAsync("/NonExistentController");

		// Assert
		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
	}

	[Test]
	public async Task Application_ShouldHandleInvalidHttpMethods()
	{
		// Act
		var response = await Client.PatchAsync("/Yandex", null);

		// Assert
		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
	}

	[Test]
	public async Task Application_ShouldSetCorrectContentType()
	{
		// Act
		var response = await Client.GetAsync("/Yandex");

		// Assert
		Assert.That(response.Content.Headers.ContentType?.MediaType, 
			Is.EqualTo("application/json"));
	}
}