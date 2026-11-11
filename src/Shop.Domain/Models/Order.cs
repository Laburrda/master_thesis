using Shop.Domain.Enums;

namespace Shop.Domain.Models
{
    public class Order
    {
        public Guid Id { get; set; }
        public long UserId {get; set;}
        public long ProductId { get; set; }
        public int Quantity { get; set; }
        public OrderStatus Status { get; set; }
        public RejectedReason? RejectedReason { get; set; }
        public required string IdempotencyKey { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ProcessedAt { get; set; }


    }
}
