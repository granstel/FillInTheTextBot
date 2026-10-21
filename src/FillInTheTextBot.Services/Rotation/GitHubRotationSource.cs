using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.Extensions;

namespace FillInTheTextBot.Services.Rotation
{
    public sealed class GitHubRotationSource(HttpClient client, RotationConfiguration configuration) : IRotationSource
    {
        private const string UserAgent = "FillInTheTextBot";

        public async Task<RotationSourceResult<string[]>> GetStoryFilesAsync(string etag, CancellationToken cancellationToken)
        {
            using var request = CreateRequest(configuration.StoriesUrl, etag);

            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            if (!string.IsNullOrEmpty(configuration.GitHubToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration.GitHubToken);
            }

            using var response = await client.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return RotationSourceResult<string[]>.NotModified;
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var files = ParseTree(json);

            var result = new RotationSourceResult<string[]>(true, files, response.Headers.ETag?.ToString());

            return result;
        }

        public async Task<RotationSourceResult<string>> GetRulesAsync(string etag, CancellationToken cancellationToken)
        {
            using var request = CreateRequest(configuration.RulesUrl, etag);

            using var response = await client.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return RotationSourceResult<string>.NotModified;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new RotationSourceResult<string>(true, null, null);
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            var result = new RotationSourceResult<string>(true, json, response.Headers.ETag?.ToString());

            return result;
        }

        private static HttpRequestMessage CreateRequest(string url, string etag)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            request.Headers.UserAgent.ParseAdd(UserAgent);

            if (!string.IsNullOrEmpty(etag))
            {
                request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(etag));
            }

            return request;
        }

        private static string[] ParseTree(string json)
        {
            var tree = json.Deserialize<GitHubTree>();

            if (tree?.Tree is null)
            {
                throw new InvalidOperationException("В ответе GitHub нет списка файлов");
            }

            if (tree.Truncated)
            {
                throw new InvalidOperationException("GitHub вернул неполный список файлов");
            }

            var files = tree.Tree
                .Where(item => item.Type == "blob")
                .Select(item => item.Path)
                .ToArray();

            return files;
        }
    }
}
