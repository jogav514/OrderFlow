using Microsoft.EntityFrameworkCore;
using Orders.Api.Entities;

namespace Orders.Api.Data;

/// <summary>
/// Contexto de base de datos exclusivo de la API de Pedidos (OrderFlow_OrdersDb).
/// Administra los pedidos, el catálogo de SKUs válidos y los eventos de respuesta ya procesados.
/// </summary>
public class OrdersDbContext : DbContext
{
    /// <summary>
    /// Recibe las opciones de configuración (cadena de conexión, proveedor SQL Server) vía inyección de dependencias.
    /// </summary>
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options)
    {
    }

    /// <summary>Tabla principal de pedidos y su ciclo de vida (Pending, Confirmed, Rejected).</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <summary>Catálogo local de referencia para validar si un SKU existe al crear un pedido.</summary>
    public DbSet<CatalogProduct> CatalogProducts => Set<CatalogProduct>();

    /// <summary>Registro de eventos StockReserved/StockRejected procesados para evitar reprocesos.</summary>
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    /// <summary>
    /// Configura el mapeo de entidades, conversiones de enums y catálogo inicial (seed).
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ClienteNombre).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Sku).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Cantidad).IsRequired();

            // Guarda el enum como texto ("Pending", "Confirmed", "Rejected") para mayor claridad en DB
            entity.Property(x => x.Estado)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.MotivoRechazo).HasMaxLength(250);
            entity.Property(x => x.CreadoEn).IsRequired();
        });

        modelBuilder.Entity<CatalogProduct>(entity =>
        {
            entity.ToTable("CatalogProducts");
            entity.HasKey(x => x.Sku);
            entity.Property(x => x.Sku).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(150).IsRequired();

            // Seed del catálogo sincronizado con los SKUs iniciales de Inventory.Worker
            entity.HasData(
                new CatalogProduct { Sku = "SKU-001", Nombre = "Laptop Pro 14" },
                new CatalogProduct { Sku = "SKU-002", Nombre = "Monitor 27 4K" },
                new CatalogProduct { Sku = "SKU-003", Nombre = "Teclado Mecanico" },
                new CatalogProduct { Sku = "SKU-004", Nombre = "Mouse Inalambrico" }
            );
        });

        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.ToTable("ProcessedEvents");
            entity.HasKey(x => x.EventId);
            entity.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ProcessedAt).IsRequired();
        });
    }
}