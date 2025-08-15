using E_Commerce.DtoModels.OrderDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.Services;

namespace E_Commerce.Interfaces
{
	public interface IOrderServices
	{
		public  Task<Result<List<OrderListDto>>> FilterOrdersAsync(
		  string? userId = null,
		  bool? deleted = null,
		  int page = 1,
		  int pageSize = 10,
		  OrderStatus? status = null);
		public  Task<Result<int>> UpdateOrderAfterPaid(int orderId, OrderStatus newStatus);
		Task<Result<OrderDto>> GetOrderByIdAsync(int orderId, string userId, bool isAdmin = false);
		public  Task<Result<OrderDto>> GetOrderByNumberAsync(string orderNumber, string userId, bool isAdmin = false);
		Task<Result<List<OrderListDto>>> GetCustomerOrdersAsync(string userId,bool isDeleted, int page = 1, int pageSize = 10);
		public Task<Result<OrderWithPaymentDto>> CreateOrderFromCartAsync(string userId, CreateOrderDto orderDto);
		Task<Result<bool>> UpdateOrderStatusAsync(int orderId, UpdateOrderStatusDto statusDto);
		Task<Result<string>> CancelOrderAsync(int orderId, CancelOrderDto cancelDto, string userId);
		Task<Result<string>> ShipOrderAsync(int orderId);
		Task<Result<string>> DeliverOrderAsync(int orderId);
		Task<Result<int?>> GetOrderCountByCustomerAsync(string userId);
		Task<Result<decimal>> GetTotalRevenueByCustomerAsync(string userId);

		public Task UpdateOrderItemsForProductsAfterAddDiscountAsync(List<int> productIds, decimal discountPercent);
		public  Task UpdateOrderItemsForProductsAfterRemoveDiscountAsync(List<int> productIds);



		Task<Result<int?>> GetTotalOrderCountAsync(OrderStatus? status, string userRole);
	}
} 