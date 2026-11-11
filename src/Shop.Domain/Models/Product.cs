

namespace Shop.Domain.Models
{
    public class Product
    {
        public long Id { get; init; }
        public required string Name { get; init; }
        public required string Category { get; init; }
        public decimal Price { get; init; }
        public int StockInitial { get; init; }
        public int StockAvailable { get; init; }
    }
}
