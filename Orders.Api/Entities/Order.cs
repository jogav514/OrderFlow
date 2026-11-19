using System.ComponentModel.DataAnnotations;

namespace Orders.Api.Entities;

public class Order
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string ClienteNombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Sku { get; set; } = string.Empty;

    public int Cantidad { get; set; }

    public OrderStatus Estado { get; set; } = OrderStatus.Pending;

    [MaxLength(250)]
    public string? MotivoRechazo { get; set; }

    public DateTime CreadoEn { get; set; }
}