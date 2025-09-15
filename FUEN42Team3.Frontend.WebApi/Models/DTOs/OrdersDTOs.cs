using System;
using System.Collections.Generic;

namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class OrderItemDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string? Image { get; set; }
    }

    public class OrderListDto
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // pending|confirmed|processing|shipped|delivered|cancelled|returned
        public string PaymentStatus { get; set; } = "pending"; // pending|paid|failed|refunded
        public DateTime CreatedAt { get; set; }
        public decimal Total { get; set; }
        public int TotalItems { get; set; }
        public IEnumerable<OrderItemDto> Items { get; set; } = Array.Empty<OrderItemDto>();
    }

    public class UpdateOrderStatusDto
    {
        public string Status { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }
}
