namespace Shop.Application.Features.Products
{
    public record ProductDto(
        long Id, 
        string Name, 
        string Category, 
        decimal Price, 
        int StockAvailable
     );
}
