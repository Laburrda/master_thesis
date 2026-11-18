using Microsoft.AspNetCore.Mvc;
using Shop.Api.Contracts.Errors;
using Shop.Application.Common;
using Shop.Application.Features.Products;
using Shop.Application.Features.Products.Enums;
using System.ComponentModel.DataAnnotations;

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
                        ErrorCodes.ProductNotFound,
                        $"Product with ID: {id} does not exist."
                    )));

            return Ok(product);
        }

        private static bool TryParseSortField(string value, out ProductSortField field)
        {
            switch (value)
            {
                case "name": field = ProductSortField.Name; return true;
                case "price": field = ProductSortField.Price; return true;
                default: field = default; return false;
            }
        }

        private static bool TryParseDirection(string order, out SortDirection direction)
        {
            switch (order)
            {
                case "asc": direction = SortDirection.Ascending; return true;
                case "desc": direction = SortDirection.Descending; return true;
                default: direction = default; return false;
            }
        }

        private BadRequestObjectResult ValidationError(string message) =>
            BadRequest(new ErrorResponse(new ErrorDetails(ErrorCodes.ValidationError, message)));

        [HttpGet]
        public async Task<ActionResult<SearchResult<ProductListItemDto>>> List(
            [FromQuery, Range(1, int.MaxValue, ErrorMessage = "page must be 1 or greater.")] int page = 1,
            [FromQuery, Range(1, 100)] int pageSize = 20,
            [FromQuery] string sort = "name",
            [FromQuery] string order = "asc", 
            [FromQuery] string? category = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            CancellationToken ct = default)
        {

            if (!TryParseSortField(sort, out var sortField))
                return ValidationError($"Unexpected parameter: {sort} as a sorting field.");

            if (!TryParseDirection(order, out var direction))
                return ValidationError($"Unexpected parameter: {direction} as a sorting direction.");

            var criteria = new ProductListCriteria(
                page, pageSize, sortField, direction, 
                category, minPrice, maxPrice);

            var result = await _listProducts.ExecuteAsync(criteria, ct);
            return Ok(result);
        }
    
    }

}

