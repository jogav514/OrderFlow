namespace Orders.Api.DTOs;

/// <summary>
/// Contrato de salida de un pedido expuesto por la API REST.
/// </summary>
public record OrderResponse(
    Guid Id,
    string ClienteNombre,
    string Sku,
    int Cantidad,
    string Estado,
    string? MotivoRechazo,
    DateTime CreadoEn
);