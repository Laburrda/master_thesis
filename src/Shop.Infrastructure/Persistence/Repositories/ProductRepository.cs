using Microsoft.EntityFrameworkCore;
using Shop.Application.Abstractions;
using Shop.Domain.Models;


namespace Shop.Infrastructure.Persistence.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly ShopDbContext _context;

        public ProductRepository(ShopDbContext context) => _context = context;

        public async Task<Product?> FindByIdAsync(long id, CancellationToken ct)
        {
            return await _context.Products
                .AsNoTracking() // As all updates are made by sql queries
                .FirstOrDefaultAsync(p => p.Id == id, ct);

        }
    }
}
