namespace OrderFlow.Contracts.Messaging;

/// <summary>
/// Centraliza los nombres de exchange, colas y routing keys de RabbitMQ
/// para garantizar consistencia entre publicadores y consumidores.
/// </summary>
public static class RabbitMqTopology
{
    public const string ExchangeName = "orderflow.events";

    public const string OrderCreatedQueue = "order.created";
    public const string StockReservedQueue = "stock.reserved";
    public const string StockRejectedQueue = "stock.rejected";

    public const string OrderCreatedRoutingKey = "order.created";
    public const string StockReservedRoutingKey = "stock.reserved";
    public const string StockRejectedRoutingKey = "stock.rejected";
}