using System.ComponentModel.DataAnnotations;

namespace Orders.Api.Entities;

public class CatalogProduct
{
    [Key]
    [MaxLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Nombre { get; set; } = string.Empty;
}