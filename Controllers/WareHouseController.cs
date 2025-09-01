using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.InventoryDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.WareHouseDtos;
using E_Commerce.Services;
using E_Commerce.Models;
using E_Commerce.UOW;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using E_Commerce.Interfaces;
using E_Commerce.Enums;
using E_Commerce.DtoModels.CategoryDtos;
using Microsoft.AspNetCore.Authorization;
using System.Transactions;
using System.Linq;
using E_Commerce.DtoModels;
using System.IdentityModel.Tokens.Jwt;
using E_Commerce.Services.WareHouseServices;
using E_Commerce.DtoModels.Responses;
using E_Commerce.ErrorHnadling;
using E_Commerce.Services.EmailServices;

namespace E_Commerce.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	[Authorize]
	public class WareHousesController : ControllerBase
	{
		private readonly IWareHouseServices _wareHouseServices;
		private readonly IProductInventoryService _productInventoryService;
		private readonly ILogger<WareHousesController> _logger;

		private WareHousesController(
			IProductInventoryService productInventoryService,
			IWareHouseServices wareHouseServices, 
			ILogger<WareHousesController> logger,
			ErrorNotificationService errorNotificationService)
			
		{
			_productInventoryService = productInventoryService ?? throw new ArgumentNullException(nameof(productInventoryService));
			_wareHouseServices = wareHouseServices ?? throw new ArgumentNullException(nameof(wareHouseServices));
		}

		[HttpGet]
		[ActionName(nameof(GetAll))]
		private async Task<ActionResult<ApiResponse<List<WareHouseDto>>>> GetAll()
		{
			try
			{
				_logger.LogInformation($"Executing {nameof(GetAll)} in WareHouseController");
				var response = await _wareHouseServices.GetAllWareHousesAsync();
				return Ok(response);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(GetAll)}");
				return Ok(ApiResponse<List<WareHouseDto>>.CreateErrorResponse("Error", new ErrorResponse("Error", ex.Message), 500));
			}
		}		

		[HttpGet("{id}")]
		[ActionName(nameof(GetByIdAsync))]
		private async Task<ActionResult<ApiResponse<WareHouseDto>>> GetByIdAsync([FromRoute] int id)
		{
			try
			{
				_logger.LogInformation($"Executing {nameof(GetByIdAsync)} in WareHouseController");

				if (id <= 0)
				{
					return BadRequest(ApiResponse<WareHouseDto>.CreateErrorResponse("Invalid Input", new ErrorResponse("Invalid Input", "Warehouse ID must be greater than 0"), 400));
				}

				var response = await _wareHouseServices.GetWareHouseByIdAsync(id);
				return Ok(response);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(GetByIdAsync)}");
				return Ok(ApiResponse<WareHouseDto>.CreateErrorResponse("Error", new ErrorResponse("Error", ex.Message), 500));
			}
		}

		[HttpPost]
		[ActionName(nameof(CreateWareHouseAsync))]
		private async Task<ActionResult<ApiResponse<WareHouseDto>>> CreateWareHouseAsync([FromBody] WareHouseDto model)
		{
			try
			{
				_logger.LogInformation($"Executing {nameof(CreateWareHouseAsync)}");

				if (model == null)
				{
					return BadRequest(ApiResponse<WareHouseDto>.CreateErrorResponse("Invalid Input", new ErrorResponse("Invalid Input", "Warehouse data cannot be null"), 400));
				}

				if (!ModelState.IsValid)
				{
					var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
					_logger.LogError($"Validation Errors: {string.Join(", ", errors)}");

					return BadRequest(ApiResponse<WareHouseDto>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", string.Join(';', errors)), 400));
				}

				string? userid = GetIdFromToken();
				if (string.IsNullOrEmpty(userid))
				{
					_logger.LogError("Admin ID not found, canceling create operation.");
					return Unauthorized(ApiResponse<WareHouseDto>.CreateErrorResponse("Auth", new ErrorResponse("Auth", "can't found userid in token"), 401));
				}

				var response = await _wareHouseServices.CreateWareHouseAsync(userid, model);
				return Ok(response);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(CreateWareHouseAsync)}");
				return Ok(ApiResponse<WareHouseDto>.CreateErrorResponse("Error", new ErrorResponse("Error", ex.Message), 500));
			}
		}

		[HttpPatch("{id}")]
		[ActionName(nameof(UpdateWareHouseAsync))]
		private async Task<ActionResult<ApiResponse<WareHouseDto>>> UpdateWareHouseAsync(
			[FromRoute] int id,
			[FromBody] WareHouseDto updateDto)
		{
			try
			{
				_logger.LogInformation($"Executing {nameof(UpdateWareHouseAsync)}");

				if (id <= 0)
				{
					return BadRequest(ApiResponse<WareHouseDto>.CreateErrorResponse("Invalid Input", new ErrorResponse("Invalid Input", "Warehouse ID must be greater than 0"), 400));
				}

				if (updateDto == null)
				{
					return BadRequest(ApiResponse<WareHouseDto>.CreateErrorResponse("Invalid Input", new ErrorResponse("Invalid Input", "Update data cannot be null"), 400));
				}

				if (!ModelState.IsValid)
				{
					var errors = string.Join("; ", ModelState.Values
														  .SelectMany(v => v.Errors)
														  .Select(e => e.ErrorMessage));
					_logger.LogError(errors);

					return BadRequest(ApiResponse<WareHouseDto>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
				}

				string? userid = GetIdFromToken();
				if (string.IsNullOrEmpty(userid))
				{
					_logger.LogError("Admin ID not found, canceling update operation.");
					return Unauthorized(ApiResponse<WareHouseDto>.CreateErrorResponse("Auth", new ErrorResponse("Auth", "can't found userid in token"), 401));
				}

				var response = await _wareHouseServices.UpdateWareHouseAsync(id, userid, updateDto);
				return Ok(response);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(UpdateWareHouseAsync)}");
				return Ok(ApiResponse<WareHouseDto>.CreateErrorResponse("Error", new ErrorResponse("Error", ex.Message), 500));
			}
		}

		[HttpDelete("{id}")]
		[ActionName(nameof(DeleteWareHouseAsync))]
		private async Task<ActionResult<ApiResponse<string>>> DeleteWareHouseAsync([FromRoute] int id)
		{
			try
			{
				_logger.LogInformation($"Executing {nameof(DeleteWareHouseAsync)}");

				if (id <= 0)
				{
					return BadRequest(ApiResponse<string>.CreateErrorResponse("Invalid Input", new ErrorResponse("Invalid Input", "Warehouse ID must be greater than 0"), 400));
				}

				string? userid = GetIdFromToken();
				if (string.IsNullOrEmpty(userid))
				{
					_logger.LogError("Admin ID not found, canceling delete operation.");
					return Unauthorized(ApiResponse<string>.CreateErrorResponse("Auth", new ErrorResponse("Auth", "can't found userid in token"), 401));
				}

				var response = await _wareHouseServices.RemoveWareHouseAsync(id, userid);
				return Ok(response);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(DeleteWareHouseAsync)}");
				return Ok(ApiResponse<string>.CreateErrorResponse("Error", new ErrorResponse("Error", ex.Message), 500));
			}
		}

		[HttpPatch("Return-Deleted-WareHouse/{id}")]
		[ActionName(nameof(ReturnRemovedWareHouseAsync))]
		private async Task<ActionResult<ApiResponse<WareHouseDto>>> ReturnRemovedWareHouseAsync([FromRoute] int id)
		{
			try
			{
				_logger.LogInformation($"Executing {nameof(ReturnRemovedWareHouseAsync)}");

				if (id <= 0)
				{
					return BadRequest(ApiResponse<WareHouseDto>.CreateErrorResponse("Invalid Input", new ErrorResponse("Invalid Input", "Warehouse ID must be greater than 0"), 400));
				}

				string? userid = GetIdFromToken();
				if (userid is null)
				{
					_logger.LogError("Invalid token or user not authenticated");
					return Unauthorized(ApiResponse<WareHouseDto>.CreateErrorResponse("Auth", new ErrorResponse("Auth", "can't found userid in token"), 401));
				}

				var response = await _wareHouseServices.ReturnRemovedWareHouseAsync(id, userid);
				return Ok(response);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(ReturnRemovedWareHouseAsync)}");
				return Ok(ApiResponse<WareHouseDto>.CreateErrorResponse("Error", new ErrorResponse("Error", ex.Message), 500));
			}
		}

		[HttpGet("{id}/Products")]
		[ActionName(nameof(GetProductsByWareHouseId))]
		private async Task<ActionResult<ApiResponse<List<InventoryDto>>>> GetProductsByWareHouseId([FromRoute] int id)
		{
			try
			{
				_logger.LogInformation($"Execute {nameof(GetProductsByWareHouseId)} in WareHouseController");
				
				if (id <= 0)
				{
					return BadRequest(ApiResponse<List<InventoryDto>>.CreateErrorResponse("Invalid Input", new ErrorResponse("Invalid Input", "Warehouse ID must be greater than 0"), 400));
				}

				var response = await _productInventoryService.GetWarehouseInventoryAsync(id);
				return Ok(response);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(GetProductsByWareHouseId)}");
				return Ok(ApiResponse<List<InventoryDto>>.CreateErrorResponse("Error", new ErrorResponse("Error", ex.Message), 500));
			}
		}

		[HttpPatch("Transfer-All-Products/{currentWarehouseId}/{newWarehouseId}")]
		[ActionName(nameof(TransferAllProducts))]
		private async Task<ActionResult<ApiResponse<string>>> TransferAllProducts(
			[FromRoute] int currentWarehouseId,
			[FromRoute] int newWarehouseId)
		{
			try
			{
				_logger.LogInformation($"Executing {nameof(TransferAllProducts)} in WareHouseController");
				
				if (currentWarehouseId <= 0 || newWarehouseId <= 0)
				{
					return BadRequest(ApiResponse<string>.CreateErrorResponse("Invalid Input", new ErrorResponse("Invalid Input", "Warehouse IDs must be greater than 0"), 400));
				}

				if (currentWarehouseId == newWarehouseId)
				{
					return BadRequest(ApiResponse<string>.CreateErrorResponse("Invalid Operation", new ErrorResponse("Invalid Operation", "Source and destination warehouses cannot be the same"), 400));
				}

				string? userid = GetIdFromToken();
				if (string.IsNullOrEmpty(userid))
				{
					_logger.LogError("Admin ID not found, canceling transfer operation.");
					return Unauthorized(ApiResponse<string>.CreateErrorResponse("Auth", new ErrorResponse("Auth", "can't found userid in token"), 401));
				}

				const int transferAllProducts = 0;
				var response = await _wareHouseServices.TransferProductsAsync(currentWarehouseId, newWarehouseId, userid, transferAllProducts);
				return Ok(response);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in {nameof(TransferAllProducts)}");
				return Ok(ApiResponse<string>.CreateErrorResponse("Error", new ErrorResponse("Error", ex.Message), 500));
			}
		}

		private string? GetIdFromToken()
		{
			return HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
		}
	}
}
