using OrderFlow.Contracts.Events;

namespace Orders.Api.Services;

/// <summary>
/// Contrato para aplicar transiciones de estado de un pedido a partir de eventos de inventario.
/// </summary>
public interface IOrderStatusUpdater
{
    Task ApplyStockReservedAsync(StockReservedEvent @event, CancellationToken cancellationToken = default);
    Task ApplyStockRejectedAsync(StockRejectedEvent @event, CancellationToken cancellationToken = default);
}