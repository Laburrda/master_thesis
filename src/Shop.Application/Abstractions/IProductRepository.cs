using Shop.Domain.Models;

namespace Shop.Application.Abstractions
{
    public interface IProductRepository
    {
        Task<Product?> FindByIdAsync(long id, CancellationToken ct);
    }
}
