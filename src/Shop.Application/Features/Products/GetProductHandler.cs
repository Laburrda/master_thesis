using Shop.Application.Abstractions;

namespace Shop.Application.Features.Products
{
    public class GetProductHandler
    {
        private readonly IProductReader _products;

        public GetProductHandler(IProductReader products) => _products = products;

        public Task<ProductDetailsDto?> ExecuteAsync(long id, CancellationToken ct) => _products.FindByIdAsync(id, ct);
    }
}
