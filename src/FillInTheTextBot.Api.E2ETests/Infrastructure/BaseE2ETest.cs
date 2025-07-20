using System;
using System.Net.Http;
using System.Text;
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
	public void OneTimeSetUp()
	{
		Factory = new TestWebApplicationFactory<Startup>();
		Client = Factory.CreateClient();
	}

	[OneTimeTearDown]
	public void OneTimeTearDown()
	{
		Client?.Dispose();
		Factory?.Dispose();
	}

	protected async Task<HttpResponseMessage> PostJsonAsync<T>(string requestUri, T content)
	{
		var json = JsonConvert.SerializeObject(content);
		var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
		return await Client.PostAsync(requestUri, httpContent);
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