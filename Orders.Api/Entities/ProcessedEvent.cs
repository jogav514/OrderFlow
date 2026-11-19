using System.ComponentModel.DataAnnotations;

namespace Orders.Api.Entities;

public class ProcessedEvent
{
    [Key]
    public Guid EventId { get; set; }

    public Guid OrderId { get; set; }

    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    public DateTime ProcessedAt { get; set; }
}