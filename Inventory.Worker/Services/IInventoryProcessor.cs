using OrderFlow.Contracts.Events;

namespace Inventory.Worker.Services;

/// <summary>
/// Resultado del procesamiento de un evento OrderCreated.
/// </summary>
public enum ProcessOutcome
{
    Reserved,
    Rejected,
    Duplicate
}

/// <summary>
/// Encapsula el resultado de negocio y el evento de respuesta a publicar (si aplica).
/// </summary>
public record InventoryProcessResult(
    ProcessOutcome Outcome,
    StockReservedEvent? ReservedEvent = null,
    StockRejectedEvent? RejectedEvent = null
);

/// <summary>
/// Contrato del procesador transaccional e idempotente de inventario.
/// </summary>
public interface IInventoryProcessor
{
    Task<InventoryProcessResult> ProcessOrderCreatedAsync(
        OrderCreatedEvent @event,
        CancellationToken cancellationToken = default);
}