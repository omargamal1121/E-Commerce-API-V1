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
		  OrderStatus? status = null,bool IsAdmin=false);
		public  Task<Result<bool>> UpdateOrderAfterPaid(int orderId,OrderStatus orderStatus);
		Task<Result<OrderDto>> GetOrderByIdAsync(int orderId, string userId, bool isAdmin = false);
		public  Task<Result<OrderDto>> GetOrderByNumberAsync(string orderNumber, string userId, bool isAdmin = false);
		public Task<Result<OrderWithPaymentDto>> CreateOrderFromCartAsync(string userId, CreateOrderDto orderDto);
		Task<Result<bool>> ConfirmOrderAsync(int orderId, string adminId, string? notes = null);
		Task<Result<bool>> ProcessOrderAsync(int orderId, string adminId, string? notes = null);
		Task<Result<bool>> RefundOrderAsync(int orderId, string adminId, string? notes = null);
		Task<Result<bool>> ReturnOrderAsync(int orderId, string adminId, string? notes = null);
		Task<Result<bool>> ExpirePaymentAsync(int orderId, string adminId, string? notes = null);
		Task<Result<bool>> CompleteOrderAsync(int orderId, string adminId, string? notes = null);
		Task<Result<decimal>> GetTotalRevenueByDateRangeAsync(DateTime startDate, DateTime endDate);
		public Task<Result<bool>> CancelOrderByCustomerAsync(int orderId, string userId);
		public  Task<Result<bool>> CancelOrderByAdminAsync(int orderId, string adminId);

		Task<Result<bool>> ShipOrderAsync(int orderId, string userId);
		Task<Result<bool>> DeliverOrderAsync(int orderId, string userId);
		Task<Result<int?>> GetOrderCountByCustomerAsync(string userId);
		Task<Result<decimal>> GetTotalRevenueByCustomerAsync(string userId);



	
		Task<Result<int?>> GetTotalOrderCountAsync(OrderStatus? status);
	}
} 