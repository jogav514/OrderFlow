using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;
using Orders.Api.DTOs;
using Orders.Api.Entities;

namespace Orders.Api.Controllers;

/// <summary>
/// Expone los endpoints REST para crear y consultar pedidos.
/// </summary>
[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    private readonly OrdersDbContext _db;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(OrdersDbContext db, ILogger<OrdersController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Crea un nuevo pedido en estado inicial Pending tras validar los datos y la existencia del SKU.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        // Normalizamos espacios y mayúsculas del SKU para evitar fallos por formato
        var clienteNombre = request.ClienteNombre?.Trim() ?? string.Empty;
        var sku = request.Sku?.Trim().ToUpperInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(clienteNombre))
        {
            return BadRequest(new { error = "El nombre del cliente no puede estar vacío." });
        }

        if (request.Cantidad < 1 || request.Cantidad > 100)
        {
            return BadRequest(new { error = "La cantidad debe estar entre 1 y 100." });
        }

        // Validación de negocio: el SKU debe existir en el catálogo inicial
        var skuExiste = await _db.CatalogProducts
            .AnyAsync(p => p.Sku == sku, cancellationToken);

        if (!skuExiste)
        {
            return BadRequest(new { error = $"El SKU '{sku}' no existe en el catálogo." });
        }

        // Todo pedido nuevo nace en estado Pending
        var order = new Order
        {
            Id = Guid.NewGuid(),
            ClienteNombre = clienteNombre,
            Sku = sku,
            Cantidad = request.Cantidad,
            Estado = OrderStatus.Pending,
            CreadoEn = DateTime.UtcNow
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Pedido {OrderId} creado en estado Pending para SKU {Sku} (cantidad: {Cantidad}).",
            order.Id, order.Sku, order.Cantidad);

        // En la Fase 6 publicaremos aquí el evento OrderCreated hacia RabbitMQ

        var response = MapToResponse(order);
        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, response);
    }

    /// <summary>
    /// Lista todos los pedidos ordenados del más reciente al más antiguo.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<OrderResponse>>> GetOrders(CancellationToken cancellationToken)
    {
        var orders = await _db.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.CreadoEn)
            .Select(o => MapToResponse(o))
            .ToListAsync(cancellationToken);

        return Ok(orders);
    }

    /// <summary>
    /// Obtiene el detalle actual de un pedido específico por su Id.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetOrderById(Guid id, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null)
        {
            return NotFound(new { error = $"No se encontró el pedido con Id '{id}'." });
        }

        return Ok(MapToResponse(order));
    }

    private static OrderResponse MapToResponse(Order order) =>
        new(
            order.Id,
            order.ClienteNombre,
            order.Sku,
            order.Cantidad,
            order.Estado.ToString(),
            order.MotivoRechazo,
            order.CreadoEn
        );
}