using Shop.Application.Abstractions;

namespace Shop.Application.Features.Products
{
    public class ListProductsHandler
    {
        private readonly IProductReader _products;

        public ListProductsHandler(IProductReader products) => _products = products;

        public Task<SearchResult<ProductListItemDto>> ExecuteAsync(ProductListCriteria criteria, CancellationToken ct) => _products.ListAsync(criteria, ct);
    }
}
