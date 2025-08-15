using System;

namespace E_Commerce.DtoModels.InventoryDtos
{
    public class LinkDto
    {
        public string Href { get; set; }
        public string Method { get; set; }
        public string Rel { get; set; }
    }

    public class ProductLinksDto
    {
        public LinkDto Self { get; set; }
        public LinkDto Category { get; set; }
        public LinkDto Discount { get; set; }
        public LinkDto Image { get; set; }
    }

    public class InventoryLinksDto
    {
        public LinkDto Self { get; set; }
        public LinkDto Warehouse { get; set; }
        public LinkDto Product { get; set; }
        public LinkDto Update { get; set; }
        public LinkDto Delete { get; set; }
    }

    public class InventoryWithLinksDto
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
        public int WarehouseId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public ProductWithLinksDto Product { get; set; }
        public InventoryLinksDto Links { get; set; }
    }

    public class ProductWithLinksDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ProductLinksDto Links { get; set; }
    }
} 