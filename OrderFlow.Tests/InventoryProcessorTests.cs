using Inventory.Worker.Data;
using Inventory.Worker.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrderFlow.Contracts.Events;

namespace OrderFlow.Tests;

public class InventoryProcessorTests
{
    private static (InventoryDbContext Db, SqliteConnection Connection) CreateInMemoryDb()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new InventoryDbContext(options);
        db.Database.EnsureCreated(); // Carga el seed: SKU-001 (10), SKU-004 (0), etc.
        return (db, connection);
    }

    [Fact]
    public async Task ProcessOrderCreated_ShouldReserveStock_WhenStockIsSufficient()
    {
        // Arrange
        var (db, conn) = CreateInMemoryDb();
        await using var _ = db;
        await using var __ = conn;

        var processor = new InventoryProcessor(db, NullLogger<InventoryProcessor>.Instance);
        var @event = new OrderCreatedEvent(Guid.NewGuid(), Guid.NewGuid(), "SKU-001", 3, DateTime.UtcNow);

        // Act
        var result = await processor.ProcessOrderCreatedAsync(@event);

        // Assert
        Assert.Equal(ProcessOutcome.Reserved, result.Outcome);
        Assert.NotNull(result.ReservedEvent);

        var stock = await db.Stocks.AsNoTracking().SingleAsync(s => s.Sku == "SKU-001");
        Assert.Equal(7, stock.Disponible); // 10 - 3 = 7
    }

    [Fact]
    public async Task ProcessOrderCreated_ShouldReject_WhenStockIsInsufficient()
    {
        // Arrange
        var (db, conn) = CreateInMemoryDb();
        await using var _ = db;
        await using var __ = conn;

        var processor = new InventoryProcessor(db, NullLogger<InventoryProcessor>.Instance);
        var @event = new OrderCreatedEvent(Guid.NewGuid(), Guid.NewGuid(), "SKU-004", 2, DateTime.UtcNow);

        // Act
        var result = await processor.ProcessOrderCreatedAsync(@event);

        // Assert
        Assert.Equal(ProcessOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.RejectedEvent);

        var stock = await db.Stocks.AsNoTracking().SingleAsync(s => s.Sku == "SKU-004");
        Assert.Equal(0, stock.Disponible);
    }

    [Fact]
    public async Task ProcessOrderCreated_ShouldBeIdempotent_WhenSameEventArrivesTwice()
    {
        // Arrange
        var (db, conn) = CreateInMemoryDb();
        await using var _ = db;
        await using var __ = conn;

        var processor = new InventoryProcessor(db, NullLogger<InventoryProcessor>.Instance);
        var duplicateEvent = new OrderCreatedEvent(Guid.NewGuid(), Guid.NewGuid(), "SKU-001", 4, DateTime.UtcNow);

        // Act: procesamos exactamente el mismo EventId dos veces
        var firstResult = await processor.ProcessOrderCreatedAsync(duplicateEvent);
        var secondResult = await processor.ProcessOrderCreatedAsync(duplicateEvent);

        // Assert: la primera vez descuenta (10 -> 6); la segunda se ignora como Duplicate y queda en 6
        Assert.Equal(ProcessOutcome.Reserved, firstResult.Outcome);
        Assert.Equal(ProcessOutcome.Duplicate, secondResult.Outcome);

        var stock = await db.Stocks.AsNoTracking().SingleAsync(s => s.Sku == "SKU-001");
        Assert.Equal(6, stock.Disponible);
    }
}