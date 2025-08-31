using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;

namespace E_Commerce.Services.ProductServices
{
    public interface IProductVariantService
    {
        Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int productId);
        Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int productId, bool? isActive, bool? deletedOnly);
        Task<Result<ProductVariantDto>> GetVariantByIdAsync(int id);
        Task<Result<ProductVariantDto>> AddVariantAsync(int productId, CreateProductVariantDto dto, string userId);
        Task<Result<ProductVariantDto>> UpdateVariantAsync(int id, UpdateProductVariantDto dto, string userId);
        Task<Result<bool>> DeleteVariantAsync(int id, string userId);

        public Task<Result<List<ProductVariantDto>>> GetVariantsBySearchAsync(int productId, string? color = null, int? Length = null, int? wist = null, VariantSize? size = null, bool? isActive = null, bool? deletedOnly = null);
        Task<Result<bool>> ActivateVariantAsync(int id, string userId);
        Task<Result<bool>> DeactivateVariantAsync(int id, string userId);
        Task<Result<bool>> AddVariantQuantityAsync(int id, int addQuantity, string userId);
        Task<Result<bool>> RemoveVariantQuantityAsync(int id, int removeQuantity, string userId);
        Task<Result<bool>> RestoreVariantAsync(int id, string userId);
        public Task<Result<bool>> RemoveQuntityAfterOrder(int id, int quantity);
        public Task<Result<bool>> AddQuntityAfterRestoreOrder(int id, int addQuantity);
    }

    public class ProductVariantService : IProductVariantService
    {
        private readonly IProductVariantQueryService _queryService;
        private readonly IProductVariantCommandService _commandService;

        public ProductVariantService(
            IProductVariantQueryService queryService,
            IProductVariantCommandService commandService)
        {
            _queryService = queryService;
            _commandService = commandService;
        }

        #region Query Operations (Read)
        public async Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int productId)
            => await _queryService.GetProductVariantsAsync(productId);

        public async Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int productId, bool? isActive, bool? deletedOnly)
            => await _queryService.GetProductVariantsAsync(productId, isActive, deletedOnly);

        public async Task<Result<ProductVariantDto>> GetVariantByIdAsync(int id)
            => await _queryService.GetVariantByIdAsync(id);

        public async Task<Result<List<ProductVariantDto>>> GetVariantsBySearchAsync(int productId, string? color = null, int? Length = null, int? wist = null, VariantSize? size = null, bool? isActive = null, bool? deletedOnly = null)
            => await _queryService.GetVariantsBySearchAsync(productId, color, Length, wist, size, isActive, deletedOnly);
        #endregion

        #region Command Operations (Write)
        public async Task<Result<ProductVariantDto>> AddVariantAsync(int productId, CreateProductVariantDto dto, string userId)
            => await _commandService.AddVariantAsync(productId, dto, userId);

        public async Task<Result<ProductVariantDto>> UpdateVariantAsync(int id, UpdateProductVariantDto dto, string userId)
            => await _commandService.UpdateVariantAsync(id, dto, userId);

        public async Task<Result<bool>> DeleteVariantAsync(int id, string userId)
            => await _commandService.DeleteVariantAsync(id, userId);

       

        public async Task<Result<bool>> ActivateVariantAsync(int id, string userId)
            => await _commandService.ActivateVariantAsync(id, userId);

        public async Task<Result<bool>> DeactivateVariantAsync(int id, string userId)
            => await _commandService.DeactivateVariantAsync(id, userId);

        public async Task<Result<bool>> AddVariantQuantityAsync(int id, int addQuantity, string userId)
            => await _commandService.AddVariantQuantityAsync(id, addQuantity, userId);

        public async Task<Result<bool>> RemoveVariantQuantityAsync(int id, int removeQuantity, string userId)
            => await _commandService.RemoveVariantQuantityAsync(id, removeQuantity, userId);

        public async Task<Result<bool>> RestoreVariantAsync(int id, string userId)
            => await _commandService.RestoreVariantAsync(id, userId);

        public async Task<Result<bool>> RemoveQuntityAfterOrder(int id, int quantity)
            => await _commandService.RemoveQuntityAfterOrder(id, quantity);

        public async Task<Result<bool>> AddQuntityAfterRestoreOrder(int id, int addQuantity)
            => await _commandService.AddQuntityAfterRestoreOrder(id, addQuantity);
        #endregion
    }
}