using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace E_Commerce.Models
{
    public class ReturnRequest
    {
        public int Id { get; set; }
        [Required]
        public int OrderId { get; set; }
        public Order Order { get; set; }
        [Required]
        public string CustomerId { get; set; }
        public Customer Customer { get; set; }
        public DateTime RequestDate { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Pending";
        public string Reason { get; set; }
        public string? AdminComment { get; set; }
        public ICollection<ReturnRequestProduct> ReturnRequestProducts { get; set; } = new List<ReturnRequestProduct>();
    }
} 