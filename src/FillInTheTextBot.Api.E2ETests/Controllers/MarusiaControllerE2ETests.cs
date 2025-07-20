using System.Net;
using System.Threading.Tasks;
using FillInTheTextBot.Api.E2ETests.Infrastructure;
using MailRu.Marusia.Models;
using MailRu.Marusia.Models.Input;
using NUnit.Framework;

namespace FillInTheTextBot.Api.E2ETests.Controllers;

[TestFixture]
[Category("E2E")]
public class MarusiaControllerE2ETests : BaseE2ETest
{
	private const string BaseRoute = "/Marusia";

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
		var inputModel = CreateValidMarusiaInputModel();

		// Act
		var response = await PostJsonAsync($"{BaseRoute}", inputModel);

		// Assert
		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		
		var result = await ReadAsAsync<OutputModel>(response);
		Assert.That(result, Is.Not.Null);
		Assert.That(result.Response, Is.Not.Null);
	}

	[Test]
	public async Task WebHook_WithToken_ShouldReturnSuccessResponse()
	{
		// Arrange
		var inputModel = CreateValidMarusiaInputModel();
		var token = "test-token";

		// Act
		var response = await PostJsonAsync($"{BaseRoute}/{token}", inputModel);

		// Assert
		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		
		var result = await ReadAsAsync<OutputModel>(response);
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

	private static InputModel CreateValidMarusiaInputModel()
	{
		return new InputModel
		{
			Request = new Request
			{
				Command = "test command",
				OriginalUtterance = "test command",
				Type = "SimpleUtterance"
			},
			Session = new InputSession
			{
				SessionId = "test-session-id",
				MessageId = 1,
				UserId = "test-user-id"
			},
			Version = "1.0"
		};
	}
}