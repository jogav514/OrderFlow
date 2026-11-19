namespace OrderFlow.Contracts.Events;

/// <summary>
/// Evento publicado por Inventory.Worker cuando hay stock suficiente y el descuento fue confirmado.
/// Orders.Api lo consume para cambiar el pedido de Pending a Confirmed.
/// </summary>
public record StockReservedEvent(
    Guid EventId,
    Guid OrderId,
    string Sku,
    int Cantidad,
    DateTime OcurridoEn
);