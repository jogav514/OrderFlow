using Inventory.Worker.Data;
using Inventory.Worker.Entities;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Contracts.Events;

namespace Inventory.Worker.Services;

/// <summary>
/// Procesa reservas de inventario garantizando consistencia transaccional e idempotencia.
/// </summary>
public class InventoryProcessor : IInventoryProcessor
{
    private readonly InventoryDbContext _db;
    private readonly ILogger<InventoryProcessor> _logger;

    public InventoryProcessor(InventoryDbContext db, ILogger<InventoryProcessor> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<InventoryProcessResult> ProcessOrderCreatedAsync(
        OrderCreatedEvent @event,
        CancellationToken cancellationToken = default)
    {
        // Envolvemos verificación de idempotencia + descuento de stock en una transacción ACID.
        // Usamos el ExecutionStrategy de EF Core para que sea compatible con EnableRetryOnFailure.
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            // 1. Verificar idempotencia: si este EventId ya fue procesado, no descontar de nuevo
            var alreadyProcessed = await _db.ProcessedEvents
                .AnyAsync(e => e.EventId == @event.EventId, cancellationToken);

            if (alreadyProcessed)
            {
                _logger.LogWarning(
                    "Evento duplicado detectado e ignorado. EventId={EventId}, OrderId={OrderId}",
                    @event.EventId, @event.OrderId);

                await transaction.RollbackAsync(cancellationToken);
                return new InventoryProcessResult(ProcessOutcome.Duplicate);
            }

            // 2. Verificar existencia del SKU
            var skuExists = await _db.Stocks
                .AnyAsync(s => s.Sku == @event.Sku, cancellationToken);

            if (!skuExists)
            {
                await RegisterProcessedEventAsync(@event, cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                var notFoundEvent = new StockRejectedEvent(
                    EventId: Guid.NewGuid(),
                    OrderId: @event.OrderId,
                    Sku: @event.Sku,
                    Cantidad: @event.Cantidad,
                    Motivo: $"El producto '{@event.Sku}' no existe en inventario.",
                    OcurridoEn: DateTime.UtcNow
                );

                return new InventoryProcessResult(ProcessOutcome.Rejected, RejectedEvent: notFoundEvent);
            }

            // 3. Descuento atómico en SQL Server: solo descuenta si Disponible >= Cantidad
            var rowsAffected = await _db.Stocks
                .Where(s => s.Sku == @event.Sku && s.Disponible >= @event.Cantidad)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(s => s.Disponible, s => s.Disponible - @event.Cantidad),
                    cancellationToken);

            // 4. Registrar el EventId como procesado dentro de la misma transacción
            await RegisterProcessedEventAsync(@event, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            if (rowsAffected == 0)
            {
                _logger.LogInformation(
                    "Stock insuficiente para OrderId={OrderId}, SKU={Sku}, Cantidad={Cantidad}.",
                    @event.OrderId, @event.Sku, @event.Cantidad);

                var rejectedEvent = new StockRejectedEvent(
                    EventId: Guid.NewGuid(),
                    OrderId: @event.OrderId,
                    Sku: @event.Sku,
                    Cantidad: @event.Cantidad,
                    Motivo: $"Stock insuficiente para el producto '{@event.Sku}'.",
                    OcurridoEn: DateTime.UtcNow
                );

                return new InventoryProcessResult(ProcessOutcome.Rejected, RejectedEvent: rejectedEvent);
            }

            _logger.LogInformation(
                "Stock reservado exitosamente para OrderId={OrderId}, SKU={Sku}, Cantidad={Cantidad}.",
                @event.OrderId, @event.Sku, @event.Cantidad);

            var reservedEvent = new StockReservedEvent(
                EventId: Guid.NewGuid(),
                OrderId: @event.OrderId,
                Sku: @event.Sku,
                Cantidad: @event.Cantidad,
                OcurridoEn: DateTime.UtcNow
            );

            return new InventoryProcessResult(ProcessOutcome.Reserved, ReservedEvent: reservedEvent);
        });
    }

    private async Task RegisterProcessedEventAsync(OrderCreatedEvent @event, CancellationToken cancellationToken)
    {
        _db.ProcessedEvents.Add(new ProcessedEvent
        {
            EventId = @event.EventId,
            OrderId = @event.OrderId,
            EventType = nameof(OrderCreatedEvent),
            ProcessedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}