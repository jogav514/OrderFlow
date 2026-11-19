using System.ComponentModel.DataAnnotations;

namespace Orders.Api.DTOs;

/// <summary>
/// Datos de entrada requeridos para registrar un nuevo pedido en POST /orders.
/// </summary>
public class CreateOrderRequest
{
    [Required(ErrorMessage = "El nombre del cliente es obligatorio.")]
    [StringLength(150, MinimumLength = 1, ErrorMessage = "El nombre del cliente debe tener entre 1 y 150 caracteres.")]
    public string ClienteNombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El SKU del producto es obligatorio.")]
    [StringLength(50, ErrorMessage = "El SKU no puede superar los 50 caracteres.")]
    public string Sku { get; set; } = string.Empty;

    [Range(1, 100, ErrorMessage = "La cantidad debe estar entre 1 y 100.")]
    public int Cantidad { get; set; }
}