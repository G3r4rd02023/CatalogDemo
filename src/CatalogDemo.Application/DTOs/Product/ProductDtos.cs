using System.ComponentModel.DataAnnotations;

namespace CatalogDemo.Application.DTOs.Product;

public record ProductDto(int Id, string Name, string? Description, decimal Price, int Stock, int CategoryId, DateTime CreatedAt);

public class CreateProductDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int Stock { get; set; }

    [Required]
    public int CategoryId { get; set; }
}

public class UpdateProductDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int Stock { get; set; }

    [Required]
    public int CategoryId { get; set; }
}