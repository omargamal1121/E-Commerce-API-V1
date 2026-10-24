using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Application.Services.CacheServices
{
    public sealed class InMemoryCacheManager : ICacheManager, IDisposable
    {
        private const int DEFAULT_EXPIRY_MINUTES = 30;
        private const long DEFAULT_CACHE_SIZE = 20_000_000; 

        private sealed record CacheEntry(string Json, DateTimeOffset ExpiresAt);

        private readonly MemoryCache _cache;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _tagTokens = new();
        private readonly ConcurrentDictionary<string, string[]> _keyTags = new();
        private readonly ILogger<InMemoryCacheManager> _logger;
        private bool _disposed;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        public InMemoryCacheManager(ILogger<InMemoryCacheManager> logger, IConfiguration? configuration = null)
        {
            _logger = logger;

            long sizeLimit = DEFAULT_CACHE_SIZE;
            if (configuration is not null)
            {
                var configuredSize = configuration.GetValue<long?>("Cache:SizeLimit")
                                     ?? configuration.GetValue<long?>("Cache:MemorySizeLimit");
                if (configuredSize.HasValue && configuredSize.Value > 0)
                {
                    sizeLimit = configuredSize.Value;
                }
            }

            _cache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = sizeLimit
            });
        }

        public Task<T?> GetAsync<T>(string key)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    return Task.FromResult<T?>(default);
                }

                if (_cache.TryGetValue(key, out CacheEntry? entry) && entry is not null)
                {
                    _logger.LogDebug("Cache hit for key: {Key}", key);
                    return Task.FromResult(JsonSerializer.Deserialize<T>(entry.Json, _jsonOptions));
                }

                _logger.LogDebug("Cache miss for key: {Key}", key);
                return Task.FromResult<T?>(default);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache for key: {Key}", key);
                return Task.FromResult<T?>(default);
            }
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, string[]? tags = null)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    return Task.CompletedTask;
                }

                var json = JsonSerializer.Serialize(value, _jsonOptions);
                var ttl = expiry ?? TimeSpan.FromMinutes(DEFAULT_EXPIRY_MINUTES);

                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl,
                    Size = Math.Max(1, json.Length)
                };

                if (tags is { Length: > 0 })
                {
                    var cleanTags = tags.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToArray();
                    if (cleanTags.Length > 0)
                    {
                        foreach (var tag in cleanTags)
                        {
                            var cts = _tagTokens.GetOrAdd(tag, _ => new CancellationTokenSource());
                            options.AddExpirationToken(new CancellationChangeToken(cts.Token));
                        }
                        _keyTags[key] = cleanTags;
                    }
                    else
                    {
                        _keyTags.TryRemove(key, out _);
                    }
                }
                else
                {
                    _keyTags.TryRemove(key, out _);
                }

                options.RegisterPostEvictionCallback(OnEvicted);
                _cache.Set(key, new CacheEntry(json, DateTimeOffset.UtcNow.Add(ttl)), options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache for key: {Key}", key);
            }

            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            try
            {
                if (!string.IsNullOrEmpty(key))
                {
                    _cache.Remove(key);
                    _keyTags.TryRemove(key, out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache for key: {Key}", key);
            }

            return Task.CompletedTask;
        }

        public Task RemoveByTagAsync(string tag)
        {
            InvalidateTag(tag);
            return Task.CompletedTask;
        }

        public Task RemoveByTagsAsync(string[] tags)
        {
            try
            {
                if (tags is { Length: > 0 })
                {
                    foreach (var tag in tags)
                    {
                        InvalidateTag(tag);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache by tags");
            }

            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    return Task.FromResult(false);
                }

                return Task.FromResult(_cache.TryGetValue(key, out _));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking cache existence for key: {Key}", key);
                return Task.FromResult(false);
            }
        }

        public Task<TimeSpan?> GetTimeToLiveAsync(string key)
        {
            try
            {
                if (!string.IsNullOrEmpty(key) && _cache.TryGetValue(key, out CacheEntry? entry) && entry is not null)
                {
                    var left = entry.ExpiresAt - DateTimeOffset.UtcNow;
                    return Task.FromResult<TimeSpan?>(left > TimeSpan.Zero ? left : TimeSpan.Zero);
                }

                return Task.FromResult<TimeSpan?>(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting TTL for key: {Key}", key);
                return Task.FromResult<TimeSpan?>(null);
            }
        }

        public Task<string[]> GetTagsAsync(string key)
        {
            try
            {
                if (!string.IsNullOrEmpty(key) && _cache.TryGetValue(key, out _) && _keyTags.TryGetValue(key, out var tags))
                {
                    return Task.FromResult(tags ?? Array.Empty<string>());
                }

                return Task.FromResult(Array.Empty<string>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tags for key: {Key}", key);
                return Task.FromResult(Array.Empty<string>());
            }
        }

        private void InvalidateTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            try
            {
                if (_tagTokens.TryRemove(tag, out var cts))
                {
                    cts.Cancel();
                    _logger.LogInformation("Invalidated cache tag: {Tag}", tag);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache by tag: {Tag}", tag);
            }
        }

        private void OnEvicted(object key, object? value, EvictionReason reason, object? state)
        {
            if (reason != EvictionReason.Replaced && key is string keyStr)
            {
                _keyTags.TryRemove(keyStr, out _);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cache.Dispose();

            foreach (var cts in _tagTokens.Values)
            {
                try
                {
                    cts.Dispose();
                }
                catch
                {
                    // Ignore disposal errors during cleanup
                }
            }

            _tagTokens.Clear();
            _keyTags.Clear();
        }
    }
}
