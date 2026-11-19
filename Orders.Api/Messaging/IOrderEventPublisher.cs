using OrderFlow.Contracts.Events;

namespace Orders.Api.Messaging;

/// <summary>
/// Contrato de publicación de eventos de pedidos.
/// Permite desacoplar OrdersController de RabbitMQ y facilita las pruebas unitarias.
/// </summary>
public interface IOrderEventPublisher
{
    Task PublishOrderCreatedAsync(OrderCreatedEvent @event, CancellationToken cancellationToken = default);
}