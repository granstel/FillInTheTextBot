using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.Rotation;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests.Rotation
{
    [TestFixture]
    public class GitHubRotationSourceTests
    {
        private const string StoriesUrl = "https://api.github.test/trees";
        private const string RulesUrl = "https://raw.github.test/rotation.json";

        private StubHandler _handler;
        private RotationConfiguration _configuration;
        private GitHubRotationSource _target;

        [SetUp]
        public void InitTest()
        {
            _handler = new StubHandler();
            _configuration = new RotationConfiguration { StoriesUrl = StoriesUrl, RulesUrl = RulesUrl };

            _target = new GitHubRotationSource(new HttpClient(_handler), _configuration);
        }

        [TearDown]
        public void CleanUp()
        {
            _handler.Dispose();
        }

        [Test]
        public async Task GetStoryFilesAsync_Tree_BlobPathsAndETag()
        {
            _handler.Respond(HttpStatusCode.OK, """
                                                {
                                                  "tree": [
                                                    { "path": "1-A-text.json", "type": "blob" },
                                                    { "path": "nested", "type": "tree" }
                                                  ],
                                                  "truncated": false
                                                }
                                                """, "W/\"abc\"");


            var result = await _target.GetStoryFilesAsync(null, CancellationToken.None);


            Assert.That(result.IsModified, Is.True);
            Assert.That(result.Value, Is.EqualTo(new[] { "1-A-text.json" }));
            Assert.That(result.ETag, Is.EqualTo("W/\"abc\""));
            Assert.That(_handler.Request.RequestUri, Is.EqualTo(new Uri(StoriesUrl)));
            Assert.That(_handler.Request.Headers.UserAgent.ToString(), Is.Not.Empty);
            Assert.That(_handler.Request.Headers.Authorization, Is.Null);
        }

        [Test]
        public void GetStoryFilesAsync_Truncated_Throws()
        {
            _handler.Respond(HttpStatusCode.OK, """{ "tree": [], "truncated": true }""");


            Assert.ThrowsAsync<InvalidOperationException>(() => _target.GetStoryFilesAsync(null, CancellationToken.None));
        }

        [Test]
        public void GetStoryFilesAsync_NoTree_Throws()
        {
            _handler.Respond(HttpStatusCode.OK, """{ "message": "Not Found" }""");


            Assert.ThrowsAsync<InvalidOperationException>(() => _target.GetStoryFilesAsync(null, CancellationToken.None));
        }

        [Test]
        public async Task GetStoryFilesAsync_ETagAndToken_ConditionalAuthorizedRequest()
        {
            _configuration.GitHubToken = "token";
            _handler.Respond(HttpStatusCode.NotModified);


            var result = await _target.GetStoryFilesAsync("W/\"abc\"", CancellationToken.None);


            Assert.That(result.IsModified, Is.False);
            Assert.That(_handler.Request.Headers.IfNoneMatch.ToString(), Is.EqualTo("W/\"abc\""));
            Assert.That(_handler.Request.Headers.Authorization, Is.EqualTo(new AuthenticationHeaderValue("Bearer", "token")));
        }

        [Test]
        public void GetStoryFilesAsync_ServerError_Throws()
        {
            _handler.Respond(HttpStatusCode.Forbidden);


            Assert.ThrowsAsync<HttpRequestException>(() => _target.GetStoryFilesAsync(null, CancellationToken.None));
        }

        [Test]
        public async Task GetRulesAsync_Found_ContentWithoutToken()
        {
            _configuration.GitHubToken = "token";
            _handler.Respond(HttpStatusCode.OK, "{}", "\"rules\"");


            var result = await _target.GetRulesAsync(null, CancellationToken.None);


            Assert.That(result.IsModified, Is.True);
            Assert.That(result.Value, Is.EqualTo("{}"));
            Assert.That(result.ETag, Is.EqualTo("\"rules\""));
            Assert.That(_handler.Request.RequestUri, Is.EqualTo(new Uri(RulesUrl)));
            Assert.That(_handler.Request.Headers.Authorization, Is.Null);
        }

        [Test]
        public async Task GetRulesAsync_NotFound_ModifiedWithoutContent()
        {
            _handler.Respond(HttpStatusCode.NotFound);


            var result = await _target.GetRulesAsync("\"rules\"", CancellationToken.None);


            Assert.That(result.IsModified, Is.True);
            Assert.That(result.Value, Is.Null);
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private HttpStatusCode _statusCode;
            private string _content;
            private string _etag;

            public HttpRequestMessage Request { get; private set; }

            public void Respond(HttpStatusCode statusCode, string content = "", string etag = null)
            {
                _statusCode = statusCode;
                _content = content;
                _etag = etag;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Request = request;

                var response = new HttpResponseMessage(_statusCode) { Content = new StringContent(_content) };

                if (_etag is not null)
                {
                    response.Headers.ETag = EntityTagHeaderValue.Parse(_etag);
                }

                return Task.FromResult(response);
            }
        }
    }
}
