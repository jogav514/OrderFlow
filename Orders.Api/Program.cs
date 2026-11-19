using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;
using Orders.Api.Messaging;
using Orders.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Leer la cadena de conexión desde la configuración (inyectada por variable de entorno)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Falta la variable de entorno 'ConnectionStrings__DefaultConnection' para Orders.Api.");
}

// 2. Registrar OrdersDbContext en el contenedor de Inyección de Dependencias (Scoped por petición HTTP)
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null)));

// 3. Servicios estándar de API REST y documentación Swagger
builder.Services.Configure<RabbitMqOptions>(
builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddScoped<IOrderEventPublisher, RabbitMqOrderEventPublisher>();
builder.Services.AddScoped<IOrderStatusUpdater, OrderStatusUpdater>();
builder.Services.AddHostedService<InventoryEventsConsumer>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 4. Aplicar migraciones y seed automáticamente al iniciar la aplicación (con reintentos para Docker)
await ApplyMigrationsWithRetryAsync(app.Services, app.Logger);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>
/// Crea la base de datos OrderFlow_OrdersDb (si no existe) y aplica migraciones pendientes.
/// Reintenta hasta 5 veces por si SQL Server aún está terminando de arrancar en Docker.
/// </summary>
static async Task ApplyMigrationsWithRetryAsync(IServiceProvider services, ILogger logger)
{
    const int maxRetries = 5;
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            await db.Database.MigrateAsync();
            logger.LogInformation("Migraciones de OrdersDb aplicadas correctamente.");
            return;
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            logger.LogWarning(ex,
                "Intento {Attempt}/{MaxRetries} fallido al migrar OrdersDb. Reintentando en 5 segundos...",
                attempt, maxRetries);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}