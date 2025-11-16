using System;

namespace ApplicationLayer.DtoModels.InventoryDtos
{
    public class InventoryLinkBuilder
    {
        private readonly string _baseUrl;
        private readonly InventoryWithLinksDto _inventory;

        public InventoryLinkBuilder(string baseUrl, InventoryWithLinksDto inventory)
        {
            _baseUrl = baseUrl;
            _inventory = inventory;
        }

        public InventoryWithLinksDto Build()
        {
            _inventory.Links = BuildInventoryLinks();
            _inventory.Product.Links = BuildProductLinks();
            return _inventory;
        }

        private InventoryLinksDto BuildInventoryLinks()
        {
            return new InventoryLinksDto
            {
                Self = CreateLink($"/api/ProductInventories/{_inventory.Id}", "GET", "self"),
                Warehouse = CreateLink($"/api/Warehouses/{_inventory.WarehouseId}", "GET", "warehouse"),
                Product = CreateLink($"/api/Products/{_inventory.Product.Id}", "GET", "product"),
                Update = CreateLink($"/api/ProductInventories/{_inventory.Id}", "PATCH", "update"),
                Delete = CreateLink($"/api/ProductInventories/{_inventory.Id}", "DELETE", "delete")
            };
        }

        private ProductLinksDto BuildProductLinks()
        {
            return new ProductLinksDto
            {
                Self = CreateLink($"/api/Products/{_inventory.Product.Id}", "GET", "self"),
                Category = CreateLink($"/api/Categories/{_inventory.Product.Id}", "GET", "category"),
                Discount = CreateLink($"/api/Discounts/{_inventory.Product.Id}", "GET", "discount"),
                Image = CreateLink($"/api/Products/{_inventory.Product.Id}/image", "GET", "image")
            };
        }

        private LinkDto CreateLink(string path, string method, string rel)
        {
            return new LinkDto
            {
                Href = $"{_baseUrl}{path}",
                Method = method,
                Rel = rel
            };
        }
    }
} 

