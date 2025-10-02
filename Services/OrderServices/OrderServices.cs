using E_Commerce.DtoModels.OrderDtos;
using E_Commerce.Enums;
using E_Commerce.Interfaces;

namespace E_Commerce.Services.Order
{
	public class OrderServices : IOrderServices
	{
        private readonly IOrderCommandService _orderCommandService;
        private readonly IOrderQueryService _orderQueryService;

		public OrderServices(
            IOrderCommandService orderCommandService,
            IOrderQueryService orderQueryService)
        {
            _orderCommandService = orderCommandService;
            _orderQueryService = orderQueryService;
        }

        // Command Operations
        public Task<Result<OrderAfterCreatedto>> CreateOrderFromCartAsync(string userId, CreateOrderDto orderDto)
            => _orderCommandService.CreateOrderFromCartAsync(userId, orderDto);

        public Task<Result<bool>> UpdateOrderAfterPaid(int orderId, OrderStatus orderStatus)
            => _orderCommandService.UpdateOrderAfterPaid(orderId, orderStatus);

		public Task<Result<bool>> ConfirmOrderAsync(int orderId, string adminId, bool IsSysyem = false,bool IsAdmin = false, string? notes = null)
            => _orderCommandService.ConfirmOrderAsync(orderId, adminId,IsSysyem,IsAdmin, notes);

		public Task<Result<bool>> ProcessOrderAsync(int orderId, string adminId, string? notes = null)
            => _orderCommandService.ProcessOrderAsync(orderId, adminId, notes);

		public Task<Result<bool>> RefundOrderAsync(int orderId, string adminId, string? notes = null)
            => _orderCommandService.RefundOrderAsync(orderId, adminId, notes);

		public Task<Result<bool>> ReturnOrderAsync(int orderId, string adminId, string? notes = null)
            => _orderCommandService.ReturnOrderAsync(orderId, adminId, notes);

		public Task<Result<bool>> ExpirePaymentAsync(int orderId, string adminId, bool IsSysyem = false,
            bool IsAdmin = false, string? notes = null)
            => _orderCommandService.ExpirePaymentAsync(orderId, adminId,IsSysyem,IsAdmin,notes);

		public Task<Result<bool>> CompleteOrderAsync(int orderId, string adminId, string? notes = null)
            => _orderCommandService.CompleteOrderAsync(orderId, adminId, notes);

		public Task<Result<bool>> ShipOrderAsync(int orderId, string adminId, string? notes = null)
            => _orderCommandService.ShipOrderAsync(orderId, adminId, notes);

		public Task<Result<bool>> DeliverOrderAsync(int orderId, string adminId, string? notes = null)
            => _orderCommandService.DeliverOrderAsync(orderId, adminId, notes);

		public Task<Result<bool>> ShipOrderAsync(int orderId, string userId)
            => _orderCommandService.ShipOrderAsync(orderId, userId);

		public Task<Result<bool>> DeliverOrderAsync(int orderId, string userId)
            => _orderCommandService.DeliverOrderAsync(orderId, userId);

        public Task<Result<bool>> CancelOrderByCustomerAsync(int orderId, string userId)
            => _orderCommandService.CancelOrderByCustomerAsync(orderId, userId);

        public Task<Result<bool>> CancelOrderByAdminAsync(int orderId, string adminId)
            => _orderCommandService.CancelOrderByAdminAsync(orderId, adminId);

        public Task ExpireUnpaidOrderInBackground(int orderId)
            => _orderCommandService.ExpireUnpaidOrderInBackground(orderId);

        public Task RestockOrderItemsInBackground(int orderId)
            => _orderCommandService.RestockOrderItemsInBackground(orderId);

        // Query Operations
        public Task<Result<OrderDto>> GetOrderByIdAsync(int orderId, string userId, bool isAdmin = false)
            => _orderQueryService.GetOrderByIdAsync(orderId, userId, isAdmin);

        public Task<Result<OrderDto>> GetOrderByNumberAsync(string orderNumber, string userId, bool isAdmin = false)
            => _orderQueryService.GetOrderByNumberAsync(orderNumber, userId, isAdmin);

        public Task<Result<int?>> GetOrderCountByCustomerAsync(string userId)
            => _orderQueryService.GetOrderCountByCustomerAsync(userId);

        public Task<Result<decimal>> GetTotalRevenueByCustomerAsync(string userId)
            => _orderQueryService.GetTotalRevenueByCustomerAsync(userId);

        public Task<Result<decimal>> GetTotalRevenueByDateRangeAsync(DateTime startDate, DateTime endDate)
            => _orderQueryService.GetTotalRevenueByDateRangeAsync(startDate, endDate);

        public Task<Result<int?>> GetTotalOrderCountAsync(OrderStatus? status)
            => _orderQueryService.GetTotalOrderCountAsync(status);

        public Task<Result<List<OrderListDto>>> FilterOrdersAsync(string? userId = null, bool? deleted = null, int page = 1, int pageSize = 10, OrderStatus? status = null, bool IsAdmin = false)
            => _orderQueryService.FilterOrdersAsync(userId, deleted, page, pageSize, status,IsAdmin);

		public async Task<Result<int>> CountOrdersAsync(OrderStatus? status = null, bool? isDelete = null, bool isAdmin = false)
		{
		    return await    _orderCommandService.CountOrdersAsync(status, isDelete, isAdmin);
		}
	}
}