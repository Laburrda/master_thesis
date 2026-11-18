namespace Shop.Application.Features.Products
{
    public record ProductListItemDto(
        long Id,
        string Name,
        string Category,
        decimal Price
     );
}
