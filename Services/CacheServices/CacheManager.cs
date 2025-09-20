using System.Text.Json;
using StackExchange.Redis;

namespace E_Commerce.Services.Cache
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

				var setTasks = new List<Task>
				{
					transaction.StringSetAsync(key, serializedValue, expiryTime)
				};

				if (tags?.Any() == true)
				{
					var keyTagsSet = $"{KEY_TAGS_PREFIX}{key}";
					setTasks.Add(transaction.SetAddAsync(keyTagsSet, tags.Select(t => (RedisValue)t).ToArray()));
					setTasks.Add(transaction.KeyExpireAsync(keyTagsSet, expiryTime));

					foreach (var tag in tags)
					{
						var tagKey = $"{TAG_PREFIX}{tag}";
						setTasks.Add(transaction.SetAddAsync(tagKey, key));
					}
				}

				bool result = await transaction.ExecuteAsync();
				if (result)
				{
					_logger.LogInformation("Cache set successfully for key: {Key} with tags: [{Tags}]",
						key, string.Join(", ", tags ?? Array.Empty<string>()));
				}
				else
				{
					_logger.LogWarning("Failed to set cache for key: {Key}", key);
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
				var tags = await _database.SetMembersAsync(keyTagsSet);
				var transaction = _database.CreateTransaction();

				_=transaction.KeyDeleteAsync(key);
				_=transaction.KeyDeleteAsync(keyTagsSet);

				foreach (var tag in tags)
				{
					var tagKey = $"{TAG_PREFIX}{tag}";
					_=transaction.SetRemoveAsync(tagKey, key);
				}

				await transaction.ExecuteAsync();
				_logger.LogInformation("Cache removed successfully for key: {Key}", key);
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
				var keys = await _database.SetMembersAsync(tagKey);

				if (keys.Length == 0)
				{
					_logger.LogWarning("No cache entries found for tag: {Tag}", tag);
					return;
				}

				var transaction = _database.CreateTransaction();
				foreach (var key in keys)
				{
					var keyStr = key.ToString();
					var keyTagsSet = $"{KEY_TAGS_PREFIX}{keyStr}";
					var relatedTags = await _database.SetMembersAsync(keyTagsSet);

					foreach (var t in relatedTags)
					{
						var fullTagKey = $"{TAG_PREFIX}{t}";
						_=transaction.SetRemoveAsync(fullTagKey, keyStr);
					}

					_ = transaction.KeyDeleteAsync(keyStr);
					_ = transaction.KeyDeleteAsync(keyTagsSet);
				}

				_ = transaction.KeyDeleteAsync(tagKey);
				await transaction.ExecuteAsync();

				_logger.LogInformation("Removed {Count} cache entries for tag: {Tag}", keys.Length, tag);
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

				var transaction = _database.CreateTransaction();
				foreach (var key in allKeys)
				{
					var keyTagsSet = $"{KEY_TAGS_PREFIX}{key}";
					var relatedTags = await _database.SetMembersAsync(keyTagsSet);

					foreach (var tag in relatedTags)
					{
						var fullTagKey = $"{TAG_PREFIX}{tag}";
						_ = transaction.SetRemoveAsync(fullTagKey, key);
					}

					_ = transaction.KeyDeleteAsync(key);
					_ = transaction.KeyDeleteAsync(keyTagsSet);
				}

				foreach (var tag in tags)
				{
					var tagKey = $"{TAG_PREFIX}{tag}";
					_ = transaction.KeyDeleteAsync(tagKey);
				}

				await transaction.ExecuteAsync();
				_logger.LogInformation("Removed {Count} cache entries for tags: [{Tags}]",
					allKeys.Count, string.Join(", ", tags));
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
				_logger.LogInformation("Checking if cache exists for key: {Key}", key);
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
				_logger.LogInformation("Getting TTL for key: {Key}", key);
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
				_logger.LogInformation("Getting tags for key: {Key}", key);
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
