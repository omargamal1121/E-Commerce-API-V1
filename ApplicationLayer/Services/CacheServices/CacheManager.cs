using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ApplicationLayer.Services.Cache
{
    public class CacheManager : ICacheManager
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _database;
        private readonly ILogger<CacheManager> _logger;
        private const int DEFAULT_EXPIRY_MINUTES = 30;
        private const string TAG_PREFIX = "tag:";
        private const string KEY_TAGS_PREFIX = "key_tags:";

        public CacheManager(IConnectionMultiplexer redis, ILogger<CacheManager> logger)
        {
            _redis = redis;
            _database = _redis.GetDatabase();
            _logger = logger;
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                _logger.LogInformation("Getting cache for key: {Key}", key);
                var value = await _database.StringGetAsync(key);

                if (value.IsNull)
                {
                    _logger.LogWarning("Cache miss for key: {Key}", key);
                    return default;
                }

                _logger.LogInformation("Cache hit for key: {Key}", key);
                return JsonSerializer.Deserialize<T>(value.ToString());
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializing cache for key: {Key}", key);
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache for key: {Key}", key);
                return default;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, string[]? tags = null)
        {
            try
            {
                _logger.LogInformation("Setting cache for key: {Key}", key);

                var serializedValue = JsonSerializer.Serialize(value);
                var expiryTime = expiry ?? TimeSpan.FromMinutes(DEFAULT_EXPIRY_MINUTES);

                var transaction = _database.CreateTransaction();

                // Add main key-value pair
                _ = transaction.StringSetAsync(key, serializedValue, expiryTime);

                // Handle tags if provided
                if (tags?.Any() == true)
                {
                    var keyTagsSet = $"{KEY_TAGS_PREFIX}{key}";

                    // Store tags associated with this key
                    _ = transaction.SetAddAsync(keyTagsSet, tags.Select(t => (RedisValue)t).ToArray());
                    _ = transaction.KeyExpireAsync(keyTagsSet, expiryTime);

                    // Add key to each tag's set
                    foreach (var tag in tags)
                    {
                        var tagKey = $"{TAG_PREFIX}{tag}";
                        _ = transaction.SetAddAsync(tagKey, key);
                    }
                }

                bool result = await transaction.ExecuteAsync();

                if (result)
                {
                    _logger.LogInformation("Cache set successfully for key: {Key} with tags: [{Tags}]",
                        key, tags?.Any() == true ? string.Join(", ", tags) : "none");
                }
                else
                {
                    _logger.LogWarning("Transaction failed while setting cache for key: {Key}", key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache for key: {Key}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                _logger.LogInformation("Removing cache for key: {Key}", key);

                var keyTagsSet = $"{KEY_TAGS_PREFIX}{key}";

                // Read tags BEFORE creating transaction
                var tags = await _database.SetMembersAsync(keyTagsSet);

                var transaction = _database.CreateTransaction();

                // Delete the main key and its tag set
                _ = transaction.KeyDeleteAsync(key);
                _ = transaction.KeyDeleteAsync(keyTagsSet);

                // Remove key from all tag sets
                foreach (var tag in tags)
                {
                    var tagKey = $"{TAG_PREFIX}{tag}";
                    _ = transaction.SetRemoveAsync(tagKey, key);
                }

                bool result = await transaction.ExecuteAsync();

                if (result)
                {
                    _logger.LogInformation("Cache removed successfully for key: {Key}", key);
                }
                else
                {
                    _logger.LogWarning("Transaction failed while removing cache for key: {Key}", key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache for key: {Key}", key);
            }
        }

        public async Task RemoveByTagAsync(string tag)
        {
            try
            {
                _logger.LogInformation("Removing cache by tag: {Tag}", tag);

                var tagKey = $"{TAG_PREFIX}{tag}";

                // Read all keys for this tag BEFORE creating transaction
                var keys = await _database.SetMembersAsync(tagKey);

                if (keys.Length == 0)
                {
                    _logger.LogWarning("No cache entries found for tag: {Tag}", tag);
                    return;
                }

                // Read all related tags for each key BEFORE creating transaction
                var keyTagsMap = new Dictionary<string, RedisValue[]>();
                foreach (var key in keys)
                {
                    var keyStr = key.ToString();
                    var keyTagsSet = $"{KEY_TAGS_PREFIX}{keyStr}";
                    var relatedTags = await _database.SetMembersAsync(keyTagsSet);
                    keyTagsMap[keyStr] = relatedTags;
                }

                // Now create transaction with all the data we need
                var transaction = _database.CreateTransaction();

                foreach (var kvp in keyTagsMap)
                {
                    var keyStr = kvp.Key;
                    var relatedTags = kvp.Value;
                    var keyTagsSet = $"{KEY_TAGS_PREFIX}{keyStr}";

                    // Remove key from all its tag sets
                    foreach (var t in relatedTags)
                    {
                        var fullTagKey = $"{TAG_PREFIX}{t}";
                        _ = transaction.SetRemoveAsync(fullTagKey, keyStr);
                    }

                    // Delete the key and its tag set
                    _ = transaction.KeyDeleteAsync(keyStr);
                    _ = transaction.KeyDeleteAsync(keyTagsSet);
                }

                // Delete the tag set itself
                _ = transaction.KeyDeleteAsync(tagKey);

                bool result = await transaction.ExecuteAsync();

                if (result)
                {
                    _logger.LogInformation("Removed {Count} cache entries for tag: {Tag}", keys.Length, tag);
                }
                else
                {
                    _logger.LogWarning("Transaction failed while removing cache by tag: {Tag}", tag);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache by tag: {Tag}", tag);
            }
        }

        public async Task RemoveByTagsAsync(string[] tags)
        {
            try
            {
                _logger.LogInformation("Removing cache by tags: [{Tags}]", string.Join(", ", tags));

                // Collect all keys for all tags BEFORE creating transaction
                var allKeys = new HashSet<string>();
                foreach (var tag in tags)
                {
                    var tagKey = $"{TAG_PREFIX}{tag}";
                    var keys = await _database.SetMembersAsync(tagKey);
                    foreach (var key in keys)
                    {
                        allKeys.Add(key.ToString());
                    }
                }

                if (!allKeys.Any())
                {
                    _logger.LogWarning("No cache entries found for tags: [{Tags}]", string.Join(", ", tags));
                    return;
                }

                // Read all related tags for each key BEFORE creating transaction
                var keyTagsMap = new Dictionary<string, RedisValue[]>();
                foreach (var key in allKeys)
                {
                    var keyTagsSet = $"{KEY_TAGS_PREFIX}{key}";
                    var relatedTags = await _database.SetMembersAsync(keyTagsSet);
                    keyTagsMap[key] = relatedTags;
                }

                // Now create transaction
                var transaction = _database.CreateTransaction();

                foreach (var kvp in keyTagsMap)
                {
                    var key = kvp.Key;
                    var relatedTags = kvp.Value;
                    var keyTagsSet = $"{KEY_TAGS_PREFIX}{key}";

                    // Remove key from all its tag sets
                    foreach (var tag in relatedTags)
                    {
                        var fullTagKey = $"{TAG_PREFIX}{tag}";
                        _ = transaction.SetRemoveAsync(fullTagKey, key);
                    }

                    // Delete the key and its tag set
                    _ = transaction.KeyDeleteAsync(key);
                    _ = transaction.KeyDeleteAsync(keyTagsSet);
                }

                // Delete all the tag sets
                foreach (var tag in tags)
                {
                    var tagKey = $"{TAG_PREFIX}{tag}";
                    _ = transaction.KeyDeleteAsync(tagKey);
                }

                bool result = await transaction.ExecuteAsync();

                if (result)
                {
                    _logger.LogInformation("Removed {Count} cache entries for tags: [{Tags}]",
                        allKeys.Count, string.Join(", ", tags));
                }
                else
                {
                    _logger.LogWarning("Transaction failed while removing cache by tags: [{Tags}]",
                        string.Join(", ", tags));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache by tags: [{Tags}]", string.Join(", ", tags));
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                _logger.LogDebug("Checking if cache exists for key: {Key}", key);
                return await _database.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking cache existence for key: {Key}", key);
                return false;
            }
        }

        public async Task<TimeSpan?> GetTimeToLiveAsync(string key)
        {
            try
            {
                _logger.LogDebug("Getting TTL for key: {Key}", key);
                return await _database.KeyTimeToLiveAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting TTL for key: {Key}", key);
                return null;
            }
        }

        public async Task<string[]> GetTagsAsync(string key)
        {
            try
            {
                _logger.LogDebug("Getting tags for key: {Key}", key);
                var keyTagsSet = $"{KEY_TAGS_PREFIX}{key}";
                var tags = await _database.SetMembersAsync(keyTagsSet);
                return tags.Select(t => t.ToString()).ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tags for key: {Key}", key);
                return Array.Empty<string>();
            }
        }
    }
}