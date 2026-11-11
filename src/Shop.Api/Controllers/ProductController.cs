using Microsoft.AspNetCore.Mvc;
using Shop.Application.Features.Products;
using Shop.Api.Contracts.Errors;
using Shop.Application.Common;

namespace Shop.Api.Controllers
{
    [ApiController]
    [Route("online-store-api/v1/products")]
    public class ProductController : ControllerBase
    {
        private readonly GetProductQuery _getProduct;

        public ProductController(GetProductQuery getProduct) => _getProduct = getProduct;

        [HttpGet("{id:long}")]
        public async Task<ActionResult<ProductDto>> GetById(long id, CancellationToken ct)
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
    
    }
}
