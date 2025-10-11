using E_Commerce.DtoModels.Responses;
using E_Commerce.DtoModels.TokenDtos;
using E_Commerce.Services;
using E_Commerce.Services.Externallogin;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace E_Commerce.Controllers
{
	[Route("api/[controller]")]
	[ApiController]

	public class ExternalLoginController : BaseController
	{
		public readonly IExtrenalLoginService _extrenalLoginService;
		public ExternalLoginController(IExtrenalLoginService extrenalLoginService)
		{
			_extrenalLoginService = extrenalLoginService;

        }
		[HttpGet("Login")]
		public IActionResult ExternalLogin(string provider = "Google", string returnUrl = "/")
		{
            var callbackUrl = Url.Action("ExternalLoginCallback", "ExternalLogin", new { returnUrl },protocol: Request.Scheme);
            var properties = 	_extrenalLoginService.ExternalLogin(provider, callbackUrl);
            return  Challenge(properties, provider);
        }
        [HttpGet("Callback")]
        [ActionName("ExternalLoginCallback")]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            var result = await _extrenalLoginService.ExternalLoginCallback(returnUrl, remoteError);

            if (!result.Success)
            {
                return BadRequest(result.Message); 
            }



            var redirectUrl = $"{returnUrl?.TrimEnd('/')}/auth-success?accessToken={result.Data.Token}";

            return Redirect(redirectUrl);
        }


    }
}
