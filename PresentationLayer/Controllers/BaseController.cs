using ApplicationLayer.DtoModels.Responses;
using ApplicationLayer.ErrorHnadling;
using ApplicationLayer.Interfaces;
using ApplicationLayer.Services;

using Microsoft.AspNetCore.Mvc;


namespace DomainLayer.Controllers
{
    [ApiController]
    public abstract class BaseController : ControllerBase
    {
        protected readonly ILinkBuilder? _linkBuilder;
        protected List<string> GetModelErrors()
        {
            return ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
        }
        protected ActionResult<ApiResponse<T>> CreateErrorResponse<T>(
         string message,
         int statusCode,
         List<string>? errors = null)
        {
            var errorResponse = ApiResponse<T>.CreateErrorResponse(
                message,
                new ErrorResponse("Error", errors ?? new List<string> { message }),
                statusCode
            );
            return StatusCode(statusCode, errorResponse);
        }

        protected BaseController(ILinkBuilder? linkBuilder = null)
        {
            _linkBuilder = linkBuilder;
        }


        protected string GetUserId() =>
            HttpContext?.Items?["UserId"]?.ToString() ?? string.Empty;


        protected bool HasManagementRole() =>
      User?.IsInRole("Admin") == true || User?.IsInRole("SuperAdmin") == true || User?.IsInRole("DeliveryCompany") == true;

        protected ActionResult<ApiResponse<T>> HandleResult<T>(
          Result<T> result,
          string apiName = "",
          int? id = null)
        {
           
            var links = _linkBuilder?.MakeRelSelf(_linkBuilder.GenerateLinks(id), apiName);

            ApiResponse<T> apiResponse;

            if (result.Success)
            {
                apiResponse = ApiResponse<T>.CreateSuccessResponse(
                    result.Message,
                    result.Data,
                    result.StatusCode,
                    warnings: result.Warnings,
                    links: links);
            }
            else
            {
                var errorResponse = (result.Warnings != null && result.Warnings.Count > 0)
                    ? new ErrorResponse("Error", result.Warnings)
                    : new ErrorResponse("Error", result.Message);

                apiResponse = ApiResponse<T>.CreateErrorResponse(
                    result.Message,
                    errorResponse,
                    result.StatusCode,
                    warnings: result.Warnings,
                    links: links);
            }

            return result.StatusCode switch
            {
                200 => Ok(apiResponse),
                201 => StatusCode(201, apiResponse),
                400 => BadRequest(apiResponse),
                401 => Unauthorized(apiResponse),
                403 => Forbid(),
                404 => NotFound(apiResponse),
                409 => Conflict(apiResponse),
                500 => StatusCode(500, apiResponse),
                _ => StatusCode(result.StatusCode, apiResponse)
            };
        }

    }
}
