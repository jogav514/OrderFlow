using System.ComponentModel.DataAnnotations;

namespace Inventory.Worker.Entities;

public class StockItem
{
    [Key]
    [MaxLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Nombre { get; set; } = string.Empty;

    public int Disponible { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}