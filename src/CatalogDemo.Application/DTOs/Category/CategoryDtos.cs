using System.ComponentModel.DataAnnotations;

namespace CatalogDemo.Application.DTOs.Category;

public record CategoryDto(int Id, string Name, string? Description);

public class CreateCategoryDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}

public class UpdateCategoryDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}