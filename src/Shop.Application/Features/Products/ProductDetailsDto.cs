namespace Shop.Application.Features.Products
{
    public record ProductDetailsDto(
        long Id, 
        string Name, 
        string Category, 
        decimal Price, 
        int StockAvailable
     );
}
