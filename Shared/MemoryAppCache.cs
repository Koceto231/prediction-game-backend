
using Microsoft.Extensions.Caching.Memory;

namespace BPFL.API.Shared
{
    public class MemoryAppCache : IAppCache
    {
        private readonly IMemoryCache memoryCache;
        public MemoryAppCache(IMemoryCache _memoryCache)
        {
            memoryCache = _memoryCache;
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        {
            memoryCache.TryGetValue(key, out T? value);
            return Task.FromResult(value);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
        {
            memoryCache.Set(key, value, ttl);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken ct = default)
        {
            memoryCache.Remove(key);
            return Task.CompletedTask;
        }
    }
}
