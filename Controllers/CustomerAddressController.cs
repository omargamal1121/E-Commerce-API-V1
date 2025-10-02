using E_Commerce.DtoModels.CustomerAddressDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Services;
using E_Commerce.Services.EmailServices;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace E_Commerce.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	[Authorize]
	public class CustomerAddressController : BaseController
	{
		private readonly ILogger<CustomerAddressController> _logger;
		private readonly ICustomerAddressServices _addressServices;
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly IErrorNotificationService _errorNotificationService;

		public CustomerAddressController(
			IBackgroundJobClient backgroundJobClient,
			ILogger<CustomerAddressController> logger,
			ICustomerAddressServices addressServices,
			IErrorNotificationService errorNotificationService)
		{
			_backgroundJobClient = backgroundJobClient;
			_logger = logger;
			_addressServices = addressServices;
			_errorNotificationService = errorNotificationService;
		}

	

		
		/// <summary>
		/// Get all addresses for the current customer
		/// </summary>
		[HttpGet]
		[ActionName(nameof(GetCustomerAddresses))]
		[ProducesResponseType(typeof(ApiResponse<List<CustomerAddressDto>>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<List<CustomerAddressDto>>), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(ApiResponse<List<CustomerAddressDto>>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<List<CustomerAddressDto>>>> GetCustomerAddresses()
		{
			try
			{
				_logger.LogInformation("Executing GetCustomerAddresses");
				var userId = GetUserId();
				var result = await _addressServices.GetCustomerAddressesAsync(userId);
				return HandleResult(result, nameof(GetCustomerAddresses));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in GetCustomerAddresses");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<List<CustomerAddressDto>>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving addresses"), 500));
			}
		}

		/// <summary>
		/// Get a specific address by ID
		/// </summary>
		[HttpGet("{id}")]
		[ActionName(nameof(GetAddressById))]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<CustomerAddressDto>>> GetAddressById(int id)
		{
			try
			{
				_logger.LogInformation($"Executing GetAddressById for address ID: {id}");
				var userId = GetUserId();
				var result = await _addressServices.GetAddressByIdAsync(id, userId);
				return HandleResult(result, nameof(GetAddressById), id);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetAddressById for address ID: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<CustomerAddressDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving address"), 500));
			}
		}

		/// <summary>
		/// Get the default address for the current customer
		/// </summary>
		[HttpGet("default")]
		[ActionName(nameof(GetDefaultAddress))]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<CustomerAddressDto>>> GetDefaultAddress()
		{
			try
			{
				_logger.LogInformation("Executing GetDefaultAddress");
				var userId = GetUserId();
				var result = await _addressServices.GetDefaultAddressAsync(userId);
				return HandleResult(result, nameof(GetDefaultAddress));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in GetDefaultAddress");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<CustomerAddressDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving default address"), 500));
			}
		}

		/// <summary>
		/// Create a new address for the current customer
		/// </summary>
		[HttpPost]
		[ActionName(nameof(CreateAddress))]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status201Created)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<CustomerAddressDto>>> CreateAddress([FromBody] CreateCustomerAddressDto addressDto)
		{
			try
			{
				_logger.LogInformation("Executing CreateAddress");
				
				if (!ModelState.IsValid)
				{
					var errors = GetModelErrors();
					_logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
					return BadRequest(ApiResponse<CustomerAddressDto>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
				}

				var userId = GetUserId();
				var result = await _addressServices.CreateAddressAsync(addressDto, userId);
				return HandleResult(result, nameof(CreateAddress));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in CreateAddress");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<CustomerAddressDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while creating address"), 500));
			}
		}

		/// <summary>
		/// Update an existing address
		/// </summary>
		[HttpPut("{id}")]
		[ActionName(nameof(UpdateAddress))]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<CustomerAddressDto>>> UpdateAddress(int id, [FromBody] UpdateCustomerAddressDto addressDto)
		{
			try
			{
				_logger.LogInformation($"Executing UpdateAddress for address ID: {id}");
				
				if (!ModelState.IsValid)
				{
					var errors = GetModelErrors();
					_logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
					return BadRequest(ApiResponse<CustomerAddressDto>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
				}

				var userId = GetUserId();
				var result = await _addressServices.UpdateAddressAsync(id, addressDto, userId);
				return HandleResult(result, nameof(UpdateAddress), id);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in UpdateAddress for address ID: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<CustomerAddressDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while updating address"), 500));
			}
		}

		/// <summary>
		/// Delete an address
		/// </summary>
		[HttpDelete("{id}")]
		[ActionName(nameof(DeleteAddress))]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<string>>> DeleteAddress(int id)
		{
			try
			{
				_logger.LogInformation($"Executing DeleteAddress for address ID: {id}");
				var userId = GetUserId();
				var result = await _addressServices.DeleteAddressAsync(id, userId);
				return HandleResult(result, nameof(DeleteAddress), id);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in DeleteAddress for address ID: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while deleting address"), 500));
			}
		}

		/// <summary>
		/// Set an address as default
		/// </summary>
		[HttpPost("{id}/set-default")]
		[ActionName(nameof(SetDefaultAddress))]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<string>>> SetDefaultAddress(int id)
		{
			try
			{
				_logger.LogInformation($"Executing SetDefaultAddress for address ID: {id}");
				var userId = GetUserId();
				var result = await _addressServices.SetDefaultAddressAsync(id, userId);
				return HandleResult(result, nameof(SetDefaultAddress), id);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in SetDefaultAddress for address ID: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while setting default address"), 500));
			}
		}

		/// <summary>
		/// Get addresses by type (Home, Work, Other)
		/// </summary>
		[HttpGet("type/{addressType}")]
		[ActionName(nameof(GetAddressesByType))]
		[ProducesResponseType(typeof(ApiResponse<List<CustomerAddressDto>>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<List<CustomerAddressDto>>>> GetAddressesByType(string addressType)
		{
			try
			{
				_logger.LogInformation($"Executing GetAddressesByType for type: {addressType}");
				var userId = GetUserId();
				var result = await _addressServices.GetAddressesByTypeAsync(addressType, userId);
				return HandleResult(result, nameof(GetAddressesByType));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetAddressesByType for type: {addressType}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<List<CustomerAddressDto>>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving addresses by type"), 500));
			}
		}

		/// <summary>
		/// Search addresses by term
		/// </summary>
		[HttpGet("search")]
		[ActionName(nameof(SearchAddresses))]
		[ProducesResponseType(typeof(ApiResponse<List<CustomerAddressDto>>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<List<CustomerAddressDto>>>> SearchAddresses([FromQuery] string searchTerm)
		{
			try
			{
				_logger.LogInformation($"Executing SearchAddresses with term: {searchTerm}");
				var userId = GetUserId();
				var result = await _addressServices.SearchAddressesAsync(searchTerm, userId);
				return HandleResult(result, nameof(SearchAddresses));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in SearchAddresses with term: {searchTerm}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<List<CustomerAddressDto>>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while searching addresses"), 500));
			}
		}

		/// <summary>
		/// Get address count for the current customer
		/// </summary>
		[HttpGet("count")]
		[ActionName(nameof(GetAddressCount))]
		[ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<int?>>> GetAddressCount()
		{
			try
			{
				_logger.LogInformation("Executing GetAddressCount");
				var userId = GetUserId();
				var result = await _addressServices.GetAddressCountAsync(userId);
				return HandleResult(result, nameof(GetAddressCount));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in GetAddressCount");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<int>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while getting address count"), 500));
			}
		}

		/// <summary>
		/// Get address with customer details (Admin only)
		/// </summary>
		[HttpGet("{id}/with-customer")]
		[Authorize(Roles = "Admin,SuperAdmin")]
		[ActionName(nameof(GetAddressWithCustomer))]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status500InternalServerError)]
		public async Task<ActionResult<ApiResponse<CustomerAddressDto>>> GetAddressWithCustomer(int id)
		{
			try
			{
				_logger.LogInformation($"Executing GetAddressWithCustomer for address ID: {id}");
				var userRole = HasManagementRole();
				var result = await _addressServices.GetAddressWithCustomerAsync(id, "Admin");
				return HandleResult(result, nameof(GetAddressWithCustomer), id);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetAddressWithCustomer for address ID: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return StatusCode(500, ApiResponse<CustomerAddressDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving address with customer details"), 500));
			}
		}
	}
} 