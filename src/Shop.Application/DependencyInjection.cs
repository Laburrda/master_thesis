using Microsoft.Extensions.DependencyInjection;
using Shop.Application.Features.Products;

namespace Shop.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(
            this IServiceCollection services)
        {
            services.AddScoped<GetProductQuery>();

            return services;
        }
    }
}
