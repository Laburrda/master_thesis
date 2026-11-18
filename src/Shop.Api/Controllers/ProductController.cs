using Microsoft.AspNetCore.Mvc;
using Shop.Application.Features.Products;
using Shop.Api.Contracts.Errors;
using Shop.Application.Common;
using Shop.Application.Features.Products.Enums;

namespace Shop.Api.Controllers
{
    [ApiController]
    [Route("online-store-api/v1/products")]
    public class ProductController : ControllerBase
    {
        private readonly GetProductHandler _getProduct;
        private readonly ListProductsHandler _listProducts;

        public ProductController(GetProductHandler getProduct, ListProductsHandler listProducts)
        {
            _getProduct = getProduct;
            _listProducts = listProducts;
        }


        [HttpGet("{id:long}")]
        public async Task<ActionResult<ProductDetailsDto>> GetById(long id, CancellationToken ct)
        {
            var product = await _getProduct.ExecuteAsync(id, ct);

            if (product is null)
                return NotFound(new ErrorResponse(
                    new ErrorDetails(
                        Errorcodes.ProductNotFound,
                        $"Product with ID: {id} does not exist."
                    )));

            return Ok(product);
        }

        [HttpGet]
        public async Task<ActionResult<SearchResult<ProductListItemDto>>> List(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] ProductSortField sort = ProductSortField.Name,
            [FromQuery] SortDirection order = SortDirection.Ascending, 
            [FromQuery] string? category = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            CancellationToken ct = default)
        {
            var criteria = new ProductListCriteria(
                page, pageSize, sort, order, 
                category, minPrice, maxPrice);

            var result = await _listProducts.ExecuteAsync(criteria, ct);
            return Ok(result);
        }
    
    }

}

