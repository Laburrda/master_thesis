using Shop.Application.Features.Products;

namespace Shop.Application.Abstractions
{
    public interface IProductReader
    {
        Task<ProductDetailsDto?> FindByIdAsync(long id, CancellationToken ct);

        Task<SearchResult<ProductListItemDto>> ListAsync(ProductListCriteria Criteria, CancellationToken ct);
    }
}
