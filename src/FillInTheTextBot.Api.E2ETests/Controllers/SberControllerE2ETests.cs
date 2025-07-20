using System.Net;
using System.Threading.Tasks;
using FillInTheTextBot.Api.E2ETests.Infrastructure;
using NUnit.Framework;
using Sber.SmartApp.Models;

namespace FillInTheTextBot.Api.E2ETests.Controllers;

[TestFixture]
[Category("E2E")]
public class SberControllerE2ETests : BaseE2ETest
{
	private const string BaseRoute = "/Sber";

	[Test]
	public async Task GetInfo_ShouldReturnSuccessResponse()
	{
		// Act
		var response = await Client.GetAsync(BaseRoute);

		// Assert
		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		
		var content = await ReadAsStringAsync(response);
		Assert.That(content, Is.Not.Empty);
		Assert.That(content, Does.Contain(BaseRoute));
	}

	[Test]
	public async Task WebHook_WithValidInput_ShouldReturnSuccessResponse()
	{
		// Arrange
		var request = CreateValidSberRequest();

		// Act
		var response = await PostJsonAsync($"{BaseRoute}", request);

		// Assert
		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		
		var result = await ReadAsAsync<Response>(response);
		Assert.That(result, Is.Not.Null);
	}

	[Test]
	public async Task WebHook_WithToken_ShouldReturnSuccessResponse()
	{
		// Arrange
		var request = CreateValidSberRequest();
		var token = "test-token";

		// Act
		var response = await PostJsonAsync($"{BaseRoute}/{token}", request);

		// Assert
		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		
		var result = await ReadAsAsync<Response>(response);
		Assert.That(result, Is.Not.Null);
	}

	[Test]
	public async Task WebHook_WithInvalidInput_ShouldReturnBadRequest()
	{
		// Arrange
		var invalidInput = "invalid json";

		// Act
		var response = await PostJsonAsync(BaseRoute, invalidInput);

		// Assert
		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
	}

	[Test]
	public async Task CreateWebHook_ShouldReturnSuccessResponse()
	{
		// Act
		var response = await Client.PutAsync(BaseRoute, null);

		// Assert
		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
	}

	[Test]
	public async Task DeleteWebHook_ShouldReturnSuccessResponse()
	{
		// Act
		var response = await Client.DeleteAsync(BaseRoute);

		// Assert
		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
	}

	private static Request CreateValidSberRequest()
	{
		return new Request
		{
			MessageName = "MESSAGE_TO_SKILL",
			Uuid = new Uuid { UserId = "test-user-id" }
		};
	}
}