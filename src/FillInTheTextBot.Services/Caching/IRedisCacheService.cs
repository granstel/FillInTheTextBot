using System;
using System.Threading.Tasks;

namespace FillInTheTextBot.Services.Caching
{
    public interface IRedisCacheService
    {
        bool TryGet<T>(string key, out T data, bool throwException = false);
        Task<bool> AddAsync(string key, object data, TimeSpan? timeOut = null);
        Task<bool> TryAddAsync(string key, object data, TimeSpan? timeOut = null, bool throwException = false);
    }
}
