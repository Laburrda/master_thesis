using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shop.Application.Abstractions;
using Shop.Infrastructure.Persistence;
using Shop.Infrastructure.Persistence.Repositories;

namespace Shop.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services, IConfiguration configuration)
        {
            string connectionString = configuration.GetConnectionString("Shop") 
                ?? throw new InvalidOperationException("Database connection string is not specified.");
            
            services.AddDbContext<ShopDbContext>(options =>
                options.UseNpgsql(connectionString));

            services.AddScoped<IProductRepository, ProductRepository>();

            return services;
        }
    }
}
