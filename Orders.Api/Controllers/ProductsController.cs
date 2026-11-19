using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;
using Orders.Api.DTOs;

namespace Orders.Api.Controllers;

/// <summary>
/// Expone el catálogo de productos válidos para facilitar la selección desde el frontend.
/// </summary>
[ApiController]
[Route("products")]
public class ProductsController : ControllerBase
{
    private readonly OrdersDbContext _db;

    public ProductsController(OrdersDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Retorna los productos disponibles en el catálogo.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts(CancellationToken cancellationToken)
    {
        var products = await _db.CatalogProducts
            .AsNoTracking()
            .OrderBy(p => p.Sku)
            .Select(p => new ProductDto(p.Sku, p.Nombre))
            .ToListAsync(cancellationToken);

        return Ok(products);
    }
}