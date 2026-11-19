using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrderFlow.Contracts.Events;
using Orders.Api.Controllers;
using Orders.Api.Data;
using Orders.Api.DTOs;
using Orders.Api.Messaging;

namespace OrderFlow.Tests;

public class OrdersControllerTests
{
    /// <summary>
    /// Doble de prueba (Fake) para verificar si OrdersController intentó publicar en RabbitMQ.
    /// </summary>
    private sealed class FakeOrderEventPublisher : IOrderEventPublisher
    {
        public int PublishCount { get; private set; }

        public Task PublishOrderCreatedAsync(OrderCreatedEvent @event, CancellationToken cancellationToken = default)
        {
            PublishCount++;
            return Task.CompletedTask;
        }
    }

    private static (OrdersDbContext Db, SqliteConnection Connection) CreateInMemoryDb()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new OrdersDbContext(options);
        db.Database.EnsureCreated(); // Aplica el seed de CatalogProducts
        return (db, connection);
    }

    [Fact]
    public async Task CreateOrder_ShouldReturn400_WhenSkuDoesNotExist()
    {
        // Arrange
        var (db, conn) = CreateInMemoryDb();
        await using var _ = db;
        await using var __ = conn;

        var publisher = new FakeOrderEventPublisher();
        var controller = new OrdersController(db, publisher, NullLogger<OrdersController>.Instance);

        var request = new CreateOrderRequest
        {
            ClienteNombre = "Carlos Pérez",
            Sku = "SKU-INEXISTENTE",
            Cantidad = 2
        };

        // Act
        var result = await controller.CreateOrder(request, CancellationToken.None);

        // Assert: responde 400, no guarda pedido y no publica evento
        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, publisher.PublishCount);
        Assert.Empty(db.Orders);
    }
}