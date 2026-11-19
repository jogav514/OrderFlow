using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrderFlow.Contracts.Events;
using Orders.Api.Data;
using Orders.Api.Entities;
using Orders.Api.Services;

namespace OrderFlow.Tests;

public class OrderStatusUpdaterTests
{
    private static (OrdersDbContext Db, SqliteConnection Connection) CreateInMemoryDb()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new OrdersDbContext(options);
        db.Database.EnsureCreated();
        return (db, connection);
    }

    [Fact]
    public async Task ApplyStockReserved_AndLateRejected_ShouldNotOverwriteConfirmedOrder()
    {
        // Arrange
        var (db, conn) = CreateInMemoryDb();
        await using var _ = db;
        await using var __ = conn;

        var orderId = Guid.NewGuid();
        db.Orders.Add(new Order
        {
            Id = orderId,
            ClienteNombre = "Laura Restrepo",
            Sku = "SKU-001",
            Cantidad = 1,
            Estado = OrderStatus.Pending,
            CreadoEn = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var updater = new OrderStatusUpdater(db, NullLogger<OrderStatusUpdater>.Instance);

        // Act: 1) llega confirmación, 2) llega un rechazo tardío con otro EventId
        await updater.ApplyStockReservedAsync(
            new StockReservedEvent(Guid.NewGuid(), orderId, "SKU-001", 1, DateTime.UtcNow));

        await updater.ApplyStockRejectedAsync(
            new StockRejectedEvent(Guid.NewGuid(), orderId, "SKU-001", 1, "Evento tardío", DateTime.UtcNow));

        // Assert: el pedido debe mantenerse Confirmed
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Confirmed, order.Estado);
        Assert.Null(order.MotivoRechazo);
    }
}