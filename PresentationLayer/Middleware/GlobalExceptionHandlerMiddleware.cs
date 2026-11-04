using Application.DtoModels.Responses;
using Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Net;
using System.Text.Json;

namespace E_Commerce.Middleware
{
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

        public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ImageValidationException ex)
            {
                _logger.LogWarning(ex, "Image validation failed: {Message}", ex.Message);
                await HandleValidationExceptionAsync(context, ex.Message, HttpStatusCode.BadRequest);
            }
            catch (BadHttpRequestException ex) when (ex.StatusCode == 413)
            {
                _logger.LogWarning(ex, "Request body too large");
                await HandleValidationExceptionAsync(context, "Request body too large. Maximum allowed size is 6 MB.", HttpStatusCode.RequestEntityTooLarge);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
                await HandleValidationExceptionAsync(context, "An internal server error occurred.", HttpStatusCode.InternalServerError);
            }
        }

        private static async Task HandleValidationExceptionAsync(HttpContext context, string message, HttpStatusCode statusCode)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var response = ApiResponse<string>.CreateErrorResponse(
                "Validation Error",
                new ErrorResponse("Validation", new List<string> { message }),
                (int)statusCode
            );

            var json = JsonSerializer.Serialize(response);
            await context.Response.WriteAsync(json);
        }
    }
}
