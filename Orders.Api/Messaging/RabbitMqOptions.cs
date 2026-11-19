namespace Orders.Api.Messaging;

/// <summary>
/// Parámetros de conexión a RabbitMQ cargados desde variables de entorno (sección "RabbitMq").
/// </summary>
public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}