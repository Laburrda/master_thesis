using Shop.Application.Abstractions;

namespace Shop.Application.Features.Products
{
    public class GetProductQuery
    {
        private readonly IProductRepository _products;

        public GetProductQuery(IProductRepository products) => _products = products;

        public async Task<ProductDto?> ExecuteAsync(long id, CancellationToken ct)
        {
            var product = await _products.FindByIdAsync(id, ct);
            return product is null ? null : new ProductDto(
                product.Id,
                product.Name,
                product.Category,
                product.Price,
                product.StockAvailable
            );
        }
    }
}
