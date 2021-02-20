using System;
using FillInTheTextBot.Services.Extensions;
using GranSteL.Helpers.Redis;
using GranSteL.ScopesBalancer;

namespace FillInTheTextBot.Services
{
    public class ScopesStorage : IScopesStorage
    {
        private readonly IRedisCacheService _cache;
        private readonly TimeSpan _expiration;

        public ScopesStorage(IRedisCacheService cache, TimeSpan expiration)
        {
            _cache = cache;
            _expiration = expiration;
        }

        public bool TryGetScopeKey(string invocationKey, out string scopeKey)
        {
            var cacheKey = GetCacheKey(invocationKey);

            return _cache.TryGet(cacheKey, out scopeKey);
        }

        public void Add(string invocationKey, string scopeId)
        {
            var cacheKey = GetCacheKey(invocationKey);

            _cache.AddAsync(cacheKey, scopeId, _expiration).Forget();
        }
        
        private string GetCacheKey(string invocationKey)
        {
            return $"scopes:{invocationKey}";
        }
    }
}