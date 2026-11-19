using Microsoft.EntityFrameworkCore;
using OrderFlow.Contracts.Events;
using Orders.Api.Data;
using Orders.Api.Entities;

namespace Orders.Api.Services;

/// <summary>
/// Actualiza el estado de los pedidos garantizando idempotencia y transiciones válidas (solo desde Pending).
/// </summary>
public class OrderStatusUpdater : IOrderStatusUpdater
{
    private readonly OrdersDbContext _db;
    private readonly ILogger<OrderStatusUpdater> _logger;

    public OrderStatusUpdater(OrdersDbContext db, ILogger<OrderStatusUpdater> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ApplyStockReservedAsync(StockReservedEvent @event, CancellationToken cancellationToken = default)
    {
        await UpdateStatusAsync(
            eventId: @event.EventId,
            orderId: @event.OrderId,
            eventType: nameof(StockReservedEvent),
            targetStatus: OrderStatus.Confirmed,
            rejectionReason: null,
            cancellationToken: cancellationToken);
    }

    public async Task ApplyStockRejectedAsync(StockRejectedEvent @event, CancellationToken cancellationToken = default)
    {
        await UpdateStatusAsync(
            eventId: @event.EventId,
            orderId: @event.OrderId,
            eventType: nameof(StockRejectedEvent),
            targetStatus: OrderStatus.Rejected,
            rejectionReason: @event.Motivo,
            cancellationToken: cancellationToken);
    }

    private async Task UpdateStatusAsync(
        Guid eventId,
        Guid orderId,
        string eventType,
        OrderStatus targetStatus,
        string? rejectionReason,
        CancellationToken cancellationToken)
    {
        // 1. Verificar idempotencia: si este evento ya fue procesado, no hacemos nada
        var alreadyProcessed = await _db.ProcessedEvents
            .AnyAsync(e => e.EventId == eventId, cancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogWarning("Evento {EventType} ({EventId}) duplicado; ignorado.", eventType, eventId);
            return;
        }

        // 2. Buscar el pedido correspondiente
        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
        {
            _logger.LogWarning("No se encontró el pedido {OrderId} para el evento {EventType}.", orderId, eventType);
            return;
        }

        // 3. Validar transición de máquina de estados: solo se puede pasar desde Pending
        if (order.Estado != OrderStatus.Pending)
        {
            _logger.LogWarning(
                "Transición ignorada para OrderId={OrderId}: estado actual '{Current}' no puede cambiar a '{Target}'.",
                orderId, order.Estado, targetStatus);
        }
        else
        {
            order.Estado = targetStatus;
            order.MotivoRechazo = rejectionReason;

            _logger.LogInformation("Pedido {OrderId} actualizado de Pending a {NewStatus}.",
                orderId, targetStatus);
        }

        // 4. Registrar el evento como procesado y guardar cambios
        _db.ProcessedEvents.Add(new ProcessedEvent
        {
            EventId = eventId,
            OrderId = orderId,
            EventType = eventType,
            ProcessedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}