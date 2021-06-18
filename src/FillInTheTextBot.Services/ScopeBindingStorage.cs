using System;
using FillInTheTextBot.Services.Extensions;
using GranSteL.Helpers.Redis;
using GranSteL.Tools.ScopeSelector;

namespace FillInTheTextBot.Services
{
    public class ScopeBindingStorage : IScopeBindingStorage
    {
        private readonly IRedisCacheService _cache;
        private readonly TimeSpan _expiration;

        public ScopeBindingStorage(IRedisCacheService cache)
        {
            _cache = cache;
            _expiration = TimeSpan.FromMinutes(5);
        }

        public bool TryGet(string invocationKey, out string scopeKey)
        {
            var cacheKey = GetCacheKey(invocationKey);

            using (Tracing.Trace(operationName: "Get scopeKey from cache"))
            {
                return _cache.TryGet(cacheKey, out scopeKey);
            }
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