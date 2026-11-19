namespace OrderFlow.Contracts.Events;

/// <summary>
/// Evento publicado por Orders.Api cuando un nuevo pedido es persistido en estado Pending.
/// Inventory.Worker lo consume para intentar reservar el inventario solicitado.
/// </summary>
public record OrderCreatedEvent(
    Guid EventId,
    Guid OrderId,
    string Sku,
    int Cantidad,
    DateTime OcurridoEn
);