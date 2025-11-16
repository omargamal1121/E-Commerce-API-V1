using ApplicationLayer.DtoModels.Shared;
using ApplicationLayer.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DomainLayer.Controllers;

namespace E_Commerce.LinkBuilders
{
	public class AccountLinkBuilder: BaseLinkBuilder, IAccountLinkBuilder
	{
		private readonly LinkGenerator _linkGenerator;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private const string ErrorMessage = "Link generation failed";

		protected override string ControllerName => nameof(AccountController).Replace("Controller", "");

		public AccountLinkBuilder(
			LinkGenerator linkGenerator,
			IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor, linkGenerator)
		{
			_linkGenerator = linkGenerator ?? throw new ArgumentNullException(nameof(linkGenerator));
			_httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
		}

		private LinkDto CreateLink(string actionName, string rel, string method)
		{
			if (_httpContextAccessor == null || _httpContextAccessor.HttpContext is null)
				return new LinkDto(ErrorMessage, ErrorMessage, ErrorMessage);

			var path = _linkGenerator.GetUriByAction(
				_httpContextAccessor.HttpContext,
				actionName,
				ControllerName);

			return new LinkDto(
				Href: path ?? ErrorMessage,
				Rel: rel,
				Method: method);
		}

		public override List<LinkDto> GenerateLinks(int? id = null)
		{
			return new List<LinkDto>
			{
				CreateLink(nameof(AccountController.LogoutAsync), "logout", "POST"),
				CreateLink(nameof(AccountController.LoginAsync), "login", "POST"),
				CreateLink(nameof(AccountController.DeleteAsync), "delete-account", "DELETE"),
				CreateLink(nameof(AccountController.ChangePasswordAsync), "change-password", "PATCH"),
				CreateLink(nameof(AccountController.UploadPhotoAsync), "upload-photo", "PATCH")
			};
		}
	}
}

