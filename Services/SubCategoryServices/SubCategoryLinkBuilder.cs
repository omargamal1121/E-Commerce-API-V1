using E_Commerce.Controllers;
using E_Commerce.DtoModels.Shared;
using E_Commerce.Interfaces;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using E_Commerce.Services.CategoryServcies;

namespace E_Commerce.Services.SubCategoryServices
{
    public class SubCategoryLinkBuilder : BaseLinkBuilder, ISubCategoryLinkBuilder
    {
        protected override string ControllerName => "SubCategory";

        public SubCategoryLinkBuilder(IHttpContextAccessor context, LinkGenerator generator)
            : base(context, generator) { }

        public override List<LinkDto> GenerateLinks(int? id = null)
        {
            if (_context.HttpContext == null)
                return new List<LinkDto>();

            var links = new List<LinkDto>
            {
                new LinkDto(
                    GetUriByAction(nameof(SubCategoryController.Create)) ?? "",
                    "create",
                    "POST"
                ),
               
              
               
            };

            if (id != null)
            {
               
                links.Add(
                    new LinkDto(
                        GetUriByAction(nameof(SubCategoryController.Delete), new { id }) ?? "",
                        "delete",
                        "DELETE"
                    )
                );
                links.Add(
                    new LinkDto(
                        GetUriByAction(nameof(SubCategoryController.Update), new { id }) ?? "",
                        "update",
                        "PUT"
                    )
                );
                links.Add(
                    new LinkDto(
                        GetUriByAction(nameof(SubCategoryController.Restore), new { id }) ?? "",
                        "restore",
                        "PATCH"
                    )
                );
                links.Add(
                    new LinkDto(
                        GetUriByAction(nameof(SubCategoryController.AddMainImage), new { id }) ?? "",
                        "add-main-image",
                        "POST"
                    )
                );
                links.Add(
                    new LinkDto(
                        GetUriByAction(nameof(SubCategoryController.AddImages), new { id }) ?? "",
                        "add-extra-images",
                        "POST"
                    )
                );
                links.Add(
                    new LinkDto(
                        GetUriByAction(nameof(SubCategoryController.Activate), new { subCategoryId = id }) ?? "",
                        "change-active-status",
                        "PATCH"
                    )
                );
                links.Add(
                    new LinkDto(
                        GetUriByAction(nameof(SubCategoryController.Deactivate), new { subCategoryId = id }) ?? "",
                        "change-active-status",
                        "PATCH"
                    )
                );
                links.Add(
                    new LinkDto(
                        GetUriByAction(nameof(SubCategoryController.RemoveImage), new { subCategoryId = id, imageId = 0 }) ?? "",
                        "remove-image",
                        "DELETE"
                    )
                );
            }
            return links;
        }
    }
} 