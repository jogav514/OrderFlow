namespace OrderFlow.Contracts.Events;

/// <summary>
/// Evento publicado por Inventory.Worker cuando no hay stock suficiente (o el SKU no existe).
/// Orders.Api lo consume para cambiar el pedido de Pending a Rejected.
/// </summary>
public record StockRejectedEvent(
    Guid EventId,
    Guid OrderId,
    string Sku,
    int Cantidad,
    string Motivo,
    DateTime OcurridoEn
);