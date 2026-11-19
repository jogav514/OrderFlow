using Inventory.Worker.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Worker.Data;

/// <summary>
/// Contexto de base de datos exclusivo del servicio de Inventario (OrderFlow_InventoryDb).
/// Administra el stock disponible de productos y el registro de eventos procesados para garantizar idempotencia.
/// </summary>
public class InventoryDbContext : DbContext
{
    /// <summary>
    /// Recibe las opciones de configuración (cadena de conexión, proveedor SQL Server) vía inyección de dependencias.
    /// </summary>
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
    {
    }

    /// <summary>Tabla de inventario actual por cada SKU.</summary>
    public DbSet<StockItem> Stocks => Set<StockItem>();

    /// <summary>Tabla de auditoría e idempotencia: guarda cada EventId ya procesado.</summary>
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    /// <summary>
    /// Configura reglas de tablas, restricciones en SQL Server y datos iniciales (seed).
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StockItem>(entity =>
        {
            // Garantiza a nivel de motor SQL Server que el inventario jamás pueda quedar negativo
            entity.ToTable("Stocks", t =>
                t.HasCheckConstraint("CK_Stocks_Disponible_NonNegative", "[Disponible] >= 0"));

            entity.HasKey(x => x.Sku);
            entity.Property(x => x.Sku).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Disponible).IsRequired();

             // Solo SQL Server genera RowVersion automáticamente; otros proveedores (como SQLite en tests) lo ignoran
            if (Database.IsSqlServer())
            {
                entity.Property(x => x.RowVersion).IsRowVersion();
            }
            else
            {
                entity.Ignore(x => x.RowVersion);
            }

            // Seed automático: carga 4 productos iniciales al aplicar la migración (mínimo requerido: 3)
            entity.HasData(
                new StockItem { Sku = "SKU-001", Nombre = "Laptop Pro 14", Disponible = 10 },
                new StockItem { Sku = "SKU-002", Nombre = "Monitor 27 4K", Disponible = 5 },
                new StockItem { Sku = "SKU-003", Nombre = "Teclado Mecanico", Disponible = 20 },
                new StockItem { Sku = "SKU-004", Nombre = "Mouse Inalambrico", Disponible = 0 }
            );
        });

        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.ToTable("ProcessedEvents");

            // EventId como PK impide insertar dos veces el mismo evento en concurrencia
            entity.HasKey(x => x.EventId);
            entity.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ProcessedAt).IsRequired();
        });
    }
}