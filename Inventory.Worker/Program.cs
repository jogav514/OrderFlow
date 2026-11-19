using Inventory.Worker;
using Inventory.Worker.Data;
using Inventory.Worker.Messaging;
using Inventory.Worker.Services;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// 1. Leer la cadena de conexión desde la configuración (inyectada por variable de entorno)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Falta la variable de entorno 'ConnectionStrings__DefaultConnection' para Inventory.Worker.");
}

// 2. Registrar InventoryDbContext. Cada evento que llegue creará un Scope nuevo para usar este DbContext.
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null)));

// 3. Registrar el servicio en segundo plano (Worker)
builder.Services.Configure<RabbitMqOptions>(
builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddScoped<IInventoryProcessor, InventoryProcessor>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// 4. Aplicar migraciones y seed inicial automáticamente al iniciar el Worker
var logger = host.Services.GetRequiredService<ILogger<Program>>();
await ApplyMigrationsWithRetryAsync(host.Services, logger);

host.Run();

/// <summary>
/// Crea la base de datos OrderFlow_InventoryDb (si no existe) y aplica migraciones + seed de productos.
/// </summary>
static async Task ApplyMigrationsWithRetryAsync(IServiceProvider services, ILogger logger)
{
    const int maxRetries = 5;
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await db.Database.MigrateAsync();
            logger.LogInformation("Migraciones de InventoryDb aplicadas correctamente.");
            return;
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            logger.LogWarning(ex,
                "Intento {Attempt}/{MaxRetries} fallido al migrar InventoryDb. Reintentando en 5 segundos...",
                attempt, maxRetries);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}