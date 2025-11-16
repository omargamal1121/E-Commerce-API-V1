namespace DomainLayer.Models
{
    public class ReturnRequestProduct
    {
        public int ReturnRequestId { get; set; }
        public ReturnRequest ReturnRequest { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; }
        public int Quantity { get; set; }
    }
} 