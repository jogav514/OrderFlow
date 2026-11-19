using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Contracts.Events;
using Orders.Api.Data;
using Orders.Api.DTOs;
using Orders.Api.Entities;
using Orders.Api.Messaging;

namespace Orders.Api.Controllers;

/// <summary>
/// Expone los endpoints REST para crear y consultar pedidos.
/// </summary>
[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    private readonly OrdersDbContext _db;
    private readonly IOrderEventPublisher _publisher;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        OrdersDbContext db,
        IOrderEventPublisher publisher,
        ILogger<OrdersController> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    /// <summary>
    /// Crea un pedido en estado Pending y publica el evento OrderCreated en RabbitMQ.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
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

        var skuExiste = await _db.CatalogProducts
            .AnyAsync(p => p.Sku == sku, cancellationToken);

        if (!skuExiste)
        {
            return BadRequest(new { error = $"El SKU '{sku}' no existe en el catálogo." });
        }

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

        // Publicamos el evento OrderCreated hacia RabbitMQ
        var orderCreatedEvent = new OrderCreatedEvent(
            EventId: Guid.NewGuid(),
            OrderId: order.Id,
            Sku: order.Sku,
            Cantidad: order.Cantidad,
            OcurridoEn: DateTime.UtcNow
        );

        try
        {
            await _publisher.PublishOrderCreatedAsync(orderCreatedEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            // Manejo explícito si RabbitMQ está caído al publicar
            _logger.LogError(ex,
                "El pedido {OrderId} se guardó en Pending, pero falló la publicación en RabbitMQ.",
                order.Id);

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "El pedido fue registrado en estado Pending, pero el broker de mensajería no está disponible en este momento.",
                orderId = order.Id
            });
        }

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