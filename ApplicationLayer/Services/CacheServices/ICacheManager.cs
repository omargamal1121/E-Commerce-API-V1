namespace ApplicationLayer.Services.Cache
{
	public interface ICacheManager
    {
        Task<T?> GetAsync<T>(string key);
        Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, string[]? tags = null);
        Task RemoveAsync(string key);
        Task RemoveByTagAsync(string tag);
        Task RemoveByTagsAsync(string[] tags);
        Task<bool> ExistsAsync(string key);
        Task<TimeSpan?> GetTimeToLiveAsync(string key);
        Task<string[]> GetTagsAsync(string key);
    }
}


