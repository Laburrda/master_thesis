namespace Shop.Application.Features.Products
{
    public record SearchResult<T>(
        IReadOnlyList<T> Items, 
        int Page,
        int PageSize,
        int TotalItems
    );
}
