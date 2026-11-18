using Microsoft.EntityFrameworkCore;
using Shop.Application.Abstractions;
using Shop.Application.Features.Products;
using Shop.Domain.Models;
using Shop.Application.Features.Products.Enums;


namespace Shop.Infrastructure.Persistence.Redis
{
    internal class ProductReader : IProductReader
    {
        private readonly ShopDbContext _context;

        public ProductReader(ShopDbContext context) => _context = context;

        public async Task<ProductDetailsDto?> FindByIdAsync(long id, CancellationToken ct)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new ProductDetailsDto(p.Id, p.Name, p.Category, p.Price, p.StockAvailable))
                .FirstOrDefaultAsync(ct);

        }

        public async Task<SearchResult<ProductListItemDto>> ListAsync(ProductListCriteria criteria, CancellationToken ct)
        {
            var query = _context.Products.AsNoTracking();

            if (criteria.Category is not null)
                query = query.Where(p => p.Category == criteria.Category);

            if (criteria.MinPrice is not null)
                query = query.Where(p => p.Price >= criteria.MinPrice);

            if (criteria.MaxPrice is not null)
                query = query.Where(p => p.Price <= criteria.MaxPrice);

            var total = await query.CountAsync(ct);

            IOrderedQueryable<Product> ordered = (criteria.SortBy, criteria.Direction) switch
            {
                (ProductSortField.Name, SortDirection.Ascending) => query.OrderBy(p => p.Name),
                (ProductSortField.Name, SortDirection.Descending) => query.OrderByDescending(p => p.Name),
                (ProductSortField.Price, SortDirection.Ascending) => query.OrderBy(p => p.Price),
                (ProductSortField.Price, SortDirection.Descending) => query.OrderByDescending(p => p.Price),
                _ => throw new ArgumentOutOfRangeException(nameof(criteria))
            };

            var items = await ordered
                .ThenBy(p => p.Id)
                .Skip((criteria.Page - 1) * criteria.PageSize)
                .Take(criteria.PageSize)
                .Select(p => new ProductListItemDto(p.Id, p.Name, p.Category, p.Price))
                .ToListAsync(ct);

            return new SearchResult<ProductListItemDto>(items, criteria.Page, criteria.PageSize, total);
        }
    }
}
