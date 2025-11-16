using DomainLayer.Enums;
using ApplicationLayer.DtoModels.OrderDtos;

namespace ApplicationLayer.Services.OrderService
{
    public interface IOrderCacheHelper
    {
        void ClearOrderCache();
        void NotifyAdminError(string message, string? stackTrace = null);
        
        // Order by ID cache methods
        void SetOrderByIdCacheAsync(int orderId, string userId, bool isAdmin, OrderDto order, TimeSpan? expiration = null);
        Task<OrderDto?> GetOrderByIdCacheAsync(int orderId, string userId, bool isAdmin);
        
        // Order by number cache methods
        void SetOrderByNumberCacheAsync(string orderNumber, string userId, bool isAdmin, OrderDto order, TimeSpan? expiration = null);
        Task<OrderDto?> GetOrderByNumberCacheAsync(string orderNumber, string userId, bool isAdmin);
        
        // Order count cache methods
        void SetOrderCountCacheAsync(string userId, int? count, TimeSpan? expiration = null);
        Task<int?> GetOrderCountCacheAsync(string userId);
        
        // Order revenue cache methods
        void SetOrderRevenueCacheAsync(string userId, decimal revenue, TimeSpan? expiration = null);
        Task<decimal?> GetOrderRevenueCacheAsync(string userId);
        
        // Order filter cache methods
        void SetOrderFilterCacheAsync(string? userId, bool? deleted, int page, int pageSize, OrderStatus? status, List<OrderListDto> orders,bool IsAdmin=false, TimeSpan? expiration = null);
        Task<List<OrderListDto>?> GetOrderFilterCacheAsync(string? userId, bool? deleted, int page, int pageSize,bool IsAdmin=false, OrderStatus? status=null);
    }
}


