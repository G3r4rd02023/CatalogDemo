---
name: dotnet-efcore-data-model
description: Use when the user wants to generate a data model, entities, DbContext, and SQL Server database configuration for a .NET project. Covers creating C# entity classes, installing the latest EF Core SQL Server packages, DbContext with Fluent API configurations, connection string in appsettings.json, DbContext registration in Program.cs, and running EF Core migrations and database updates. Use for "modelo de datos", "DbContext", "EF Core", "base de datos SQL Server", "migraciones", or data layer scaffolding.
version: 1.0.0
user-invocable: true
argument-hint: "[DbContextName] [DatabaseName] [--entities Entity1,Entity2]"
license: MIT
allowed-tools:
  - Bash(dotnet *)
  - Bash(powershell *)
---

# Skill: Generar modelo de datos, DbContext y configuración de BD en .NET (SQL Server)

Esta skill guía la creación de la capa de datos con **Entity Framework Core** y **SQL Server**, garantizando que se usen las **últimas versiones** del SDK de .NET, de EF Core y de las herramientas `dotnet-ef`.

## Principios

- **Última versión siempre**: detectar el SDK instalado más reciente, usar `dotnet add package` sin versión fija (resuelve la última estable) y mantener `dotnet-ef` actualizado con `dotnet tool update --global dotnet-ef`.
- **Fluent API sobre Data Annotations**: las configuraciones de entidades viven en `IEntityTypeConfiguration<T>` separadas, aplicadas automáticamente desde el ensamblado.
- **Migraciones versionadas**: cada cambio de modelo genera una migración y se aplica con `dotnet ef database update`.
- **Secretos fuera del código**: la cadena de conexión se guarda en `appsettings.json` (y `appsettings.Development.json`), nunca hardcodeada.

## Cuándo usar

- El usuario quiere crear entidades/modelo de datos para una BD SQL Server.
- El usuario necesita un `DbContext`, configuración de EF Core y migraciones.
- El usuario quiere registrar el contexto en `Program.cs` y agregar connection string.

## Cuándo NO usar

- Proyectos que ya tienen EF Core configurado y solo requieren una migración adicional (usar directamente `dotnet ef`).
- Bases de datos no SQL Server (adaptar el proveedor).

## Entradas a solicitar al usuario

Antes de ejecutar, confirmar:

1. **Proyecto destino** (ruta del `.csproj` donde se agregará la capa de datos; por defecto el proyecto WebApi/API).
2. **Nombre del DbContext** (ej. `ApplicationDbContext`).
3. **Nombre de la base de datos** (ej. `MyAppDb`).
4. **Servidor SQL** (ej. `localhost`, `.` o `localhost\SQLEXPRESS`).
5. **Entidades a crear**: para cada una, nombre y lista de propiedades con tipo (ej. `Product: Id (int), Name (string, required, max 200), Price (decimal), CreatedAt (DateTime)`).
6. **Relaciones** entre entidades (1:N, N:M, 1:1) y claves foráneas.
7. **Schema** de BD (por defecto `dbo`).
8. **¿Usar migraciones automáticas al iniciar?** (por defecto: no; solo `dotnet ef database update`).

## Paso 0 — Verificar SDK y herramientas EF

```powershell
dotnet --version
dotnet --list-sdks
```

Tomar el SDK más reciente y su TFM (`net{major}.0`).

Instalar/actualizar la herramienta global `dotnet-ef` (última versión):

```powershell
dotnet tool update --global dotnet-ef
# Si no está instalada:
# dotnet tool install --global dotnet-ef
dotnet ef --version
```

## Paso 1 — Instalar paquetes EF Core (última versión)

Ejecutar en el proyecto destino (sin `--version` para resolver la última estable):

```powershell
$project = "src/MyApp.WebApi"   # ajustar
dotnet add $project package Microsoft.EntityFrameworkCore
dotnet add $project package Microsoft.EntityFrameworkCore.SqlServer
dotnet add $project package Microsoft.EntityFrameworkCore.Design
dotnet add $project package Microsoft.EntityFrameworkCore.Tools
```

> Si el proyecto usa Administración Central de Paquetes (CPM), los paquetes se registran en `Directory.Packages.props` automáticamente.

## Paso 2 — Crear las entidades (clases C#)

Crear una carpeta `Domain/Entities` (o `Entities`) y generar cada entidad. Ejemplo base:

```csharp
// Entities/Product.cs
namespace MyApp.Domain.Entities;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Relación ejemplo 1:N
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}
```

Reglas:
- Toda entidad tiene `Id` (int o Guid) como clave primaria.
- Propiedades de navegación marcadas `= null!` para evitar warnings de nullability.
- Fechas en UTC (`DateTime.UtcNow`).
- Usar tipos anulables (`?`) para campos opcionales.

## Paso 3 — Crear configuraciones Fluent API

Crear carpeta `Infrastructure/Data/Configurations` (o `Data/Configurations`) con una clase por entidad:

```csharp
// Data/Configurations/ProductConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApp.Domain.Entities;

namespace MyApp.Infrastructure.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", "dbo");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Price)
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.Name);
    }
}
```

Aplicar el mismo patrón a cada entidad.

## Paso 4 — Crear el DbContext

```csharp
// Data/ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Data.Configurations;

namespace MyApp.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica todas las IEntityTypeConfiguration<T> del ensamblado
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Convención global opcional: nombres de tabla en plural (ya por DbSet)
    }
}
```

## Paso 5 — Agregar connection string en appsettings.json

En `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=MyAppDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

Para desarrollo, en `appsettings.Development.json` se puede usar SQL Server local o LocalDB:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=MyAppDb;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

> Para autenticación SQL (usuario/contraseña), usar: `Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True`.

## Paso 6 — Registrar el DbContext en Program.cs

```csharp
using Microsoft.EntityFrameworkCore;
using MyApp.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
            sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
        }));

// ... resto del pipeline
```

Si se usa Inyección de dependencias por capas, mover el registro a `DependencyInjection.cs` de Infrastructure:

```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
{
    services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
    return services;
}
```

Y en `Program.cs`: `builder.Services.AddInfrastructure(builder.Configuration);`

## Paso 7 — Ejecutar migración y actualizar BD

Crear la migración inicial:

```powershell
$project = "src/MyApp.WebApi"   # proyecto startup
$contextProject = "src/MyApp.Infrastructure"  # si DbContext está en otro proyecto
dotnet ef migrations add InitialCreate --project $contextProject --startup-project $project
```

Aplicar la migración a la base de datos:

```powershell
dotnet ef database update --project $contextProject --startup-project $project
```

Para cambios posteriores al modelo:

```powershell
dotnet ef migrations add <NombreDescriptivo> --project $contextProject --startup-project $project
dotnet ef database update --project $contextProject --startup-project $project
```

## Verificación

```powershell
# Listar migraciones
dotnet ef migrations list --project $contextProject --startup-project $project

# Verificar que la BD existe y las tablas se crearon (SQL Server)
# sqlcmd -S localhost -d MyAppDb -Q "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES"

# Compilar
dotnet build
```

## Checklist final

- [ ] SDK y `dotnet-ef` en su última versión.
- [ ] Paquetes EF Core SQL Server instalados (última estable).
- [ ] Entidades creadas con claves y navegaciones correctas.
- [ ] Configuraciones Fluent API por entidad.
- [ ] `DbContext` con `ApplyConfigurationsFromAssembly`.
- [ ] Connection string en `appsettings.json` (no hardcodeado).
- [ ] DbContext registrado en `Program.cs` / DI.
- [ ] Migración inicial creada y aplicada (`dotnet ef database update`).
- [ ] `dotnet build` sin errores.

## Notas y troubleshooting

- Error "No se puede crear una instancia de DbContext": asegurar que el proyecto de startup tenga referencia al proyecto que contiene el DbContext y que `--startup-project` apunte al ejecutable (WebApi).
- Error de certificado SQL Server: agregar `TrustServerCertificate=True` a la connection string.
- Si `dotnet ef` no se reconoce: instalar con `dotnet tool install --global dotnet-ef` y reiniciar la terminal.
- Para múltiples DbContext, especificar `--context <NombreDbContext>`.
- Para revertir una migración: `dotnet ef database update <MigracionAnterior>` y `dotnet ef migrations remove`.
- Para generar script SQL sin aplicar: `dotnet ef migrations script`.
