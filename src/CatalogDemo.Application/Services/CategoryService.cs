using CatalogDemo.Application.DTOs.Category;
using CatalogDemo.Application.Interfaces;
using CatalogDemo.Domain.Entities;

namespace CatalogDemo.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _repository;

    public CategoryService(ICategoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetAllAsync(ct);
        return entities.Select(c => new CategoryDto(c.Id, c.Name, c.Description)).ToList();
    }

    public async Task<CategoryDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        return entity is null ? null : new CategoryDto(entity.Id, entity.Name, entity.Description);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto, CancellationToken ct = default)
    {
        var entity = new Category { Name = dto.Name, Description = dto.Description };
        var created = await _repository.AddAsync(entity, ct);
        return new CategoryDto(created.Id, created.Name, created.Description);
    }

    public async Task<CategoryDto?> UpdateAsync(int id, UpdateCategoryDto dto, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity is null) return null;

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        await _repository.UpdateAsync(entity, ct);
        return new CategoryDto(entity.Id, entity.Name, entity.Description);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity is null) return false;
        await _repository.DeleteAsync(entity, ct);
        return true;
    }
}