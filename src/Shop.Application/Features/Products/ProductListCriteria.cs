
using Shop.Application.Features.Products.Enums;

namespace Shop.Application.Features.Products
{
    public record ProductListCriteria(
        int Page, 
        int PageSize, 
        ProductSortField SortBy, 
        SortDirection Direction, 
        string? Category, 
        decimal? MinPrice, 
        decimal? MaxPrice
     );
}
