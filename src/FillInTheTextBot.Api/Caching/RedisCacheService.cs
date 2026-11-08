using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FillInTheTextBot.Services.Caching;
using StackExchange.Redis;

namespace FillInTheTextBot.Api.Caching
{
    internal sealed class RedisCacheService(IDatabase database, string keyPrefix) : IRedisCacheService
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public bool TryGet<T>(string key, out T data, bool throwException = false)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);
            data = default;
            try
            {
                var value = database.StringGet(keyPrefix + key);
                if (value.IsNullOrEmpty)
                {
                    return false;
                }

                var text = value.ToString();
                data = text is T rawValue ? rawValue : JsonSerializer.Deserialize<T>(text, Options);
                return true;
            }
            catch (Exception) when (!throwException)
            {
                return false;
            }
        }

        public Task<bool> AddAsync(string key, object data, TimeSpan? timeOut = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);
            if (data is null)
            {
                return Task.FromResult(false);
            }

            // String values (session IDs and markers) are stored without JSON quotes.
            var value = data as string ?? JsonSerializer.Serialize(data, Options);
            Expiration expiry = timeOut is { } ttl ? ttl : Expiration.Persist;
            return database.StringSetAsync(keyPrefix + key, value, expiry: expiry);
        }

        public async Task<bool> TryAddAsync(string key, object data, TimeSpan? timeOut = null, bool throwException = false)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);
            try
            {
                return await AddAsync(key, data, timeOut);
            }
            catch (Exception) when (!throwException)
            {
                return false;
            }
        }
    }
}
