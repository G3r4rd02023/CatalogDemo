---
name: dotnet-rest-crud
description: Use when the user wants to create CRUD endpoints for a REST API in .NET. Covers requesting entities, generating DTOs, repositories, application services, controllers/endpoints, and dependency injection registration. Ensures latest .NET SDK and NuGet package versions. Use for "CRUD", "endpoints REST", "controladores API", "repositorios", "DTOs", or API scaffolding.
version: 1.0.0
user-invocable: true
argument-hint: "[EntityName] [RoutePrefix] [--dto create,update,read]"
license: MIT
allowed-tools:
  - Bash(dotnet *)
  - Bash(powershell *)
---

# Skill: Crear endpoints CRUD para una API REST en .NET

Esta skill guía la creación de un **CRUD completo** (Create, Read, Update, Delete) en una API REST .NET siguiendo Arquitectura Limpia, garantizando el uso de las **últimas versiones** del SDK y de los paquetes NuGet.

## Principios

- **Última versión siempre**: detectar el SDK más reciente, usar `dotnet add package` sin versión fija y `LangVersion=latest`.
- **Separación de capas**: Controllers (presentación) → Services (aplicación) → Repositories (infraestructura) → DbContext.
- **DTOs por operación**: no exponer entidades de dominio directamente; usar DTOs de entrada/salida.
- **Respuestas HTTP correctas**: 200/201/204/400/404/409 con `ActionResult<T>`.
- **Validación**: Data Annotations o FluentValidation en los DTOs de entrada.

## Cuándo usar

- El usuario quiere generar endpoints CRUD para una o varias entidades.
- El usuario necesita DTOs, servicios, repositorios y controladores.
- El usuario quiere registrar inyección de dependencias para la API.

## Cuándo NO usar

- APIs de solo lectura o con operaciones no CRUD (usar endpoints personalizados).
- Proyectos sin capa de datos ya configurada (primero usar `dotnet-efcore-data-model`).

## Entradas a solicitar al usuario

Antes de ejecutar, confirmar:

1. **Proyecto destino** (WebApi/API).
2. **Entidades** a exponer (deben existir en Domain).
3. **Prefijo de ruta** (por defecto `api/[controller]`).
4. **DTOs requeridos** (Create, Update, Read/List) y campos a exponer/ocultar.
5. **Paginación/búsqueda** en el endpoint de listado (por defecto: sí, con `page` y `pageSize`).
6. **Autenticación/autorización** en los endpoints (por defecto: no; opcional `[Authorize]`).
7. **Validación** a usar: Data Annotations (por defecto) o FluentValidation.

## Paso 0 — Verificar SDK y paquetes

```powershell
dotnet --version
dotnet --list-sdks
```

Instalar paquetes necesarios (última versión estable):

```powershell
$api = "src/MyApp.WebApi"
dotnet add $api package Microsoft.AspNetCore.OpenApi
# Opcional: validación con FluentValidation
# dotnet add $api package FluentValidation.AspNetCore
```

> Si se usa FluentValidation en Application, instalar también en el proyecto Application.

## Paso 1 — Crear DTOs

Carpeta: `Application/DTOs/<Entity>` (o `Features/<Entity>/DTOs`).

```csharp
// DTOs/Product/ProductDto.cs
namespace MyApp.Application.DTOs.Product;

public record ProductDto(
    int Id,
    string Name,
    string? Description,
    decimal Price,
    int Stock,
    int CategoryId,
    DateTime CreatedAt);

// DTOs/Product/CreateProductDto.cs
using System.ComponentModel.DataAnnotations;

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

// DTOs/Product/UpdateProductDto.cs
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
```

## Paso 2 — Crear repositorios

Interfaz en Application:

```csharp
// Application/Interfaces/IRepository.cs
using MyApp.Domain.Entities;

namespace MyApp.Application.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(T entity, CancellationToken ct = default);
    Task<bool> ExistsAsync(int id, CancellationToken ct = default);
}

// Application/Interfaces/IProductRepository.cs
public interface IProductRepository : IRepository<Product> { }
```

Implementación en Infrastructure:

```csharp
// Infrastructure/Repositories/Repository.cs
using Microsoft.EntityFrameworkCore;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;

namespace MyApp.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _dbSet.FindAsync(new object[] { id }, ct);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await _dbSet.AsNoTracking().ToListAsync(ct);

    public async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(T entity, CancellationToken ct = default)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken ct = default)
        => await _dbSet.FindAsync(new object[] { id }, ct) is not null;
}

// Infrastructure/Repositories/ProductRepository.cs
public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(ApplicationDbContext context) : base(context) { }
}
```

## Paso 3 — Crear servicios de aplicación

```csharp
// Application/Services/IProductService.cs
using MyApp.Application.DTOs.Product;

namespace MyApp.Application.Services;

public interface IProductService
{
    Task<ProductDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct = default);
    Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken ct = default);
    Task<ProductDto?> UpdateAsync(int id, UpdateProductDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

// Application/Services/ProductService.cs
using MyApp.Application.DTOs.Product;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;

namespace MyApp.Application.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _repository;

    public ProductService(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProductDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetAllAsync(ct);
        return entities.Select(Map).ToList();
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken ct = default)
    {
        var entity = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            Stock = dto.Stock,
            CategoryId = dto.CategoryId
        };
        var created = await _repository.AddAsync(entity, ct);
        return Map(created);
    }

    public async Task<ProductDto?> UpdateAsync(int id, UpdateProductDto dto, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity is null) return null;

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Price = dto.Price;
        entity.Stock = dto.Stock;
        entity.CategoryId = dto.CategoryId;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, ct);
        return Map(entity);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity is null) return false;
        await _repository.DeleteAsync(entity, ct);
        return true;
    }

    private static ProductDto Map(Product p) =>
        new(p.Id, p.Name, p.Description, p.Price, p.Stock, p.CategoryId, p.CreatedAt);
}
```

## Paso 4 — Crear controladores y endpoints

```csharp
// Controllers/ProductsController.cs
using Microsoft.AspNetCore.Mvc;
using MyApp.Application.DTOs.Product;
using MyApp.Application.Services;

namespace MyApp.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;

    public ProductsController(IProductService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(int id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> Update(int id, [FromBody] UpdateProductDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _service.UpdateAsync(id, dto, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await _service.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }
}
```

## Paso 5 — Registrar inyección de dependencias

En `Infrastructure/DependencyInjection.cs`:

```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
{
    services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

    services.AddScoped<IProductRepository, ProductRepository>();
    // registrar más repositorios aquí
    return services;
}
```

En `Application/DependencyInjection.cs` (o en Program.cs):

```csharp
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    services.AddScoped<IProductService, ProductService>();
    // registrar más servicios aquí
    return services;
}
```

En `Program.cs`:

```csharp
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
```

## Paso 6 — Verificación

```powershell
dotnet build
dotnet run --project src/MyApp.WebApi
```

Probar endpoints (ejemplo con curl):

```powershell
# Crear
curl -X POST https://localhost:5001/api/products -H "Content-Type: application/json" -d '{"name":"Laptop","price":999.99,"stock":10,"categoryId":1}'

# Listar
curl https://localhost:5001/api/products

# Obtener por id
curl https://localhost:5001/api/products/1

# Actualizar
curl -X PUT https://localhost:5001/api/products/1 -H "Content-Type: application/json" -d '{"name":"Laptop Pro","price":1299.99,"stock":5,"categoryId":1}'

# Eliminar
curl -X DELETE https://localhost:5001/api/products/1
```

OpenAPI/Swagger UI (si se habilita en desarrollo):

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
```

## Checklist final

- [ ] SDK y paquetes en última versión.
- [ ] DTOs creados (Create/Update/Read) sin exponer entidades.
- [ ] Repositorio genérico + específico implementados.
- [ ] Servicio de aplicación con mapeo a DTOs.
- [ ] Controlador con los 5 endpoints CRUD y códigos HTTP correctos.
- [ ] DI registrado en Application e Infrastructure.
- [ ] `dotnet build` sin errores.
- [ ] Endpoints probados (200/201/204/404).

## Notas y troubleshooting

- Error 404 en `CreatedAtAction`: asegurar que el nombre del action (`nameof(GetById)`) coincida y que la ruta incluya `{id}`.
- Error de validación 400: verificar Data Annotations en los DTOs y que `ModelState.IsValid` se evalúe.
- Error de DI "Unable to resolve service": registrar todos los repositorios/servicios en los métodos de extensión.
- Para múltiples entidades, repetir el patrón DTO → Repository → Service → Controller.
- Para paginación, agregar parámetros `page` y `pageSize` al endpoint `GetAll` y usar `Skip/Take` en el repositorio.
- Para autorización, agregar `[Authorize]` al controlador o a endpoints específicos y configurar JWT/Auth en `Program.cs`.
