using ApplicationLayer.DtoModels.Shared;
using ApplicationLayer.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace E_Commerce.LinkBuilders
{
    public class ProductLinkBuilder : BaseLinkBuilder, IProductLinkBuilder
    {
        protected override string ControllerName => "Products";

        public ProductLinkBuilder(IHttpContextAccessor context, LinkGenerator generator)
            : base(context, generator) { }

        public override List<LinkDto> GenerateLinks(int? id = null)
        {
            if (_context.HttpContext == null)
                return new List<LinkDto>();

            var links = new List<LinkDto>
            {
                new LinkDto(
                    GetUriByAction("CreateProduct") ?? "",
                    "create",
                    "POST"
                ),
            };

            if (id != null)
            {
                links.Add(new LinkDto(
                    GetUriByAction("UpdateProduct", new { id }) ?? "",
                    "update",
                    "PUT"
                ));
                links.Add(new LinkDto(
                    GetUriByAction("DeleteProduct", new { id }) ?? "",
                    "delete",
                    "DELETE"
                ));
                links.Add(new LinkDto(
                    GetUriByAction("GetProductImages", new { id }) ?? "",
                    "get-images",
                    "GET"
                ));
                links.Add(new LinkDto(
                    GetUriByAction("AddProductImages", new { id }) ?? "",
                    "add-images",
                    "POST"
                ));
                links.Add(new LinkDto(
                    GetUriByAction("UploadAndSetMainImage", new { id }) ?? "",
                    "set-main-image",
                    "POST"
                ));
            }

            return links;
        }
    }
}


