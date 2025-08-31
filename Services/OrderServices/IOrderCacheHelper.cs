using E_Commerce.DtoModels.OrderDtos;
using E_Commerce.Enums;

namespace E_Commerce.Services.Order
{
    public interface IOrderCacheHelper
    {
        void ClearOrderCache();
        void NotifyAdminError(string message, string? stackTrace = null);
        
        // Order by ID cache methods
        Task SetOrderByIdCacheAsync(int orderId, string userId, bool isAdmin, OrderDto order, TimeSpan? expiration = null);
        Task<OrderDto?> GetOrderByIdCacheAsync(int orderId, string userId, bool isAdmin);
        
        // Order by number cache methods
        Task SetOrderByNumberCacheAsync(string orderNumber, string userId, bool isAdmin, OrderDto order, TimeSpan? expiration = null);
        Task<OrderDto?> GetOrderByNumberCacheAsync(string orderNumber, string userId, bool isAdmin);
        
        // Order count cache methods
        Task SetOrderCountCacheAsync(string userId, int? count, TimeSpan? expiration = null);
        Task<int?> GetOrderCountCacheAsync(string userId);
        
        // Order revenue cache methods
        Task SetOrderRevenueCacheAsync(string userId, decimal revenue, TimeSpan? expiration = null);
        Task<decimal?> GetOrderRevenueCacheAsync(string userId);
        
        // Order filter cache methods
        Task SetOrderFilterCacheAsync(string? userId, bool? deleted, int page, int pageSize, OrderStatus? status, List<OrderListDto> orders, TimeSpan? expiration = null);
        Task<List<OrderListDto>?> GetOrderFilterCacheAsync(string? userId, bool? deleted, int page, int pageSize, OrderStatus? status);
    }
}
