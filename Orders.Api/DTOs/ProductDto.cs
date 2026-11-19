namespace Orders.Api.DTOs;

/// <summary>
/// Representa un producto disponible en el catálogo para selección en el frontend.
/// </summary>
public record ProductDto(string Sku, string Nombre);