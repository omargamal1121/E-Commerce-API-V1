using System;
using System.ComponentModel.DataAnnotations;

namespace DomainLayer.Models
{
    public class Review
    {
        public int Id { get; set; }
        [Required]
        public int ProductId { get; set; }
        public Product Product { get; set; }
        [Required]
        public string CustomerId { get; set; }
        public Customer Customer { get; set; }
        [Range(1, 5)]
        public int Rating { get; set; }
        [MaxLength(1000)]
        public string Comment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
} 