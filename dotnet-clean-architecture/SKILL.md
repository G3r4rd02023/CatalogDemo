---
name: dotnet-clean-architecture
description: Use when the user wants to create, scaffold, or set up a .NET solution and projects following Clean Architecture (onion/hexagonal). Covers solution structure (Domain, Application, Infrastructure, Web/API, tests), project references, Central Package Management, and ensuring the latest .NET SDK, target framework, language version, and NuGet package versions. Use for "crear solución .NET", "clean architecture", "arquitectura limpia", "scaffold dotnet", or new .NET backends/APIs.
version: 1.0.0
user-invocable: true
argument-hint: "[solution-name] [output-dir] [--db sqlserver|postgres|sqlite] [--tests xunit|mstest|nunit]"
license: MIT
allowed-tools:
  - Bash(dotnet *)
  - Bash(powershell *)
---

# Skill: Crear solución y proyectos para Arquitectura Limpia en .NET

Esta skill guía la creación de una solución .NET con **Arquitectura Limpia** (Clean Architecture) y garantiza que **siempre se use la última versión disponible** del SDK de .NET, del framework de destino, del lenguaje C# y de los paquetes NuGet.

## Principios

- **Última versión siempre**: detectar el SDK instalado más reciente, usar su TFM (`netX.0`), `LangVersion=latest` y resolver paquetes NuGet a su última versión estable mediante `dotnet add package` (sin fijar versión) + Administración Central de Paquetes (CPM).
- **Dependencias hacia adentro**: `Web/API -> Infrastructure -> Application -> Domain`. `Domain` no depende de nada externo.
- **Interfaces en Application**, implementaciones en Infrastructure.
- **Un solo punto de composición** (composition root) en el proyecto de presentación/API.
- Verificar al final con `dotnet build`, `dotnet test` y `dotnet list package --outdated`.

## Cuándo usar

- El usuario pide crear/scaffold de una solución .NET nueva con arquitectura limpia/onion/hexagonal.
- El usuario quiere proyectos separados para Domain, Application, Infrastructure y API.
- El usuario quiere asegurar versiones actualizadas de .NET y NuGet.

## Cuándo NO usar

- Proyectos .NET existentes que solo requieren refactor o agregar un paquete.
- Frontend, móvil o tareas no relacionadas con backend .NET.

## Entradas a confirmar con el usuario

Antes de ejecutar, confirmar (si no se especificó):

1. **Nombre de la solución** (ej. `MyApp`).
2. **Directorio de salida** (por defecto el directorio actual).
3. **Proveedor de base de datos**: `sqlserver` (por defecto), `postgres` o `sqlite`.
4. **Framework de pruebas**: `xunit` (por defecto), `mstest` o `nunit`.
5. **Incluir MediatR/CQRS** y **FluentValidation** (por defecto: sí).

## Paso 0 — Verificar y fijar la última versión del SDK

```powershell
dotnet --version
dotnet --list-sdks
```

Tomar el SDK con la versión más alta. Definir:

- `SDK_VERSION` = versión completa del SDK más reciente (ej. `10.0.402`).
- `TFM` = `net{major}.0` donde `major` es el mayor del SDK (ej. `net10.0`).

Opcionalmente crear `global.json` en la raíz para forzar el SDK más reciente:

```json
{
  "sdk": {
    "version": "<SDK_VERSION>",
    "rollForward": "latestMajor",
    "allowPrerelease": false
  }
}
```

> `rollForward: latestMajor` permite usar un SDK más nuevo si está instalado, cumpliendo "siempre la última versión".

## Paso 1 — Crear la solución

```powershell
$solutionName = "MyApp"
$root = "C:\ruta\salida\$solutionName"
New-Item -ItemType Directory -Path $root -Force | Out-Null
Set-Location $root

dotnet new sln -n $solutionName
```

Estructura de carpetas:

```
$solutionName/
├── global.json
├── Directory.Packages.props
├── .editorconfig
├── src/
│   ├── $solutionName.Domain/
│   ├── $solutionName.Application/
│   ├── $solutionName.Infrastructure/
│   └── $solutionName.WebApi/
└── tests/
    ├── $solutionName.Domain.Tests/
    ├── $solutionName.Application.Tests/
    └── $solutionName.Infrastructure.Tests/
```

Crear carpetas:

```powershell
New-Item -ItemType Directory -Path src, tests -Force | Out-Null
```

## Paso 2 — Crear los proyectos

```powershell
# Domain
dotnet new classlib -n "$solutionName.Domain" -o "src/$solutionName.Domain" -f $TFM

# Application
dotnet new classlib -n "$solutionName.Application" -o "src/$solutionName.Application" -f $TFM

# Infrastructure
dotnet new classlib -n "$solutionName.Infrastructure" -o "src/$solutionName.Infrastructure" -f $TFM

# Web API (presentación / composition root)
dotnet new webapi -n "$solutionName.WebApi" -o "src/$solutionName.WebApi" -f $TFM --use-controllers

# Tests
dotnet new xunit -n "$solutionName.Domain.Tests" -o "tests/$solutionName.Domain.Tests" -f $TFM
dotnet new xunit -n "$solutionName.Application.Tests" -o "tests/$solutionName.Application.Tests" -f $TFM
dotnet new xunit -n "$solutionName.Infrastructure.Tests" -o "tests/$solutionName.Infrastructure.Tests" -f $TFM
```

Agregar proyectos a la solución:

```powershell
dotnet sln add (Get-ChildItem -Path src, tests -Recurse -Filter *.csproj | ForEach-Object FullName)

# Normalizar los .csproj generados por plantillas: eliminar PackageReference
# con versión fija para poder usar Administración Central de Paquetes (CPM)
# sin conflictos. Los paquetes se vuelven a agregar después con su última versión.
Get-ChildItem -Path src, tests -Recurse -Filter *.csproj | ForEach-Object {
  (Get-Content $_.FullName) | Where-Object { $_ -notmatch '<PackageReference' } | Set-Content $_.FullName
}
```

## Paso 3 — Referencias entre proyectos

```powershell
# Application -> Domain
dotnet add "src/$solutionName.Application" reference "src/$solutionName.Domain"

# Infrastructure -> Application (y transitivamente Domain)
dotnet add "src/$solutionName.Infrastructure" reference "src/$solutionName.Application"

# WebApi -> Infrastructure (composition root)
dotnet add "src/$solutionName.WebApi" reference "src/$solutionName.Infrastructure"

# Tests
dotnet add "tests/$solutionName.Domain.Tests" reference "src/$solutionName.Domain"
dotnet add "tests/$solutionName.Application.Tests" reference "src/$solutionName.Application"
dotnet add "tests/$solutionName.Infrastructure.Tests" reference "src/$solutionName.Infrastructure"
```

## Paso 4 — Administración Central de Paquetes (CPM)

Crear `Directory.Packages.props` en la raíz:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <!-- Las versiones se agregan automáticamente con 'dotnet add package' -->
  </ItemGroup>
</Project>
```

Esto asegura que las versiones se declaren en un solo lugar y que `dotnet add package` resuelva la **última versión estable** disponible en NuGet.

## Paso 5 — Agregar paquetes (última versión)

Ejecutar `dotnet add package <Id>` **sin `--version`** para que NuGet resuelva la última versión estable y la registre en `Directory.Packages.props`.

### Application

```powershell
dotnet add "src/$solutionName.Application" package MediatR
dotnet add "src/$solutionName.Application" package FluentValidation
dotnet add "src/$solutionName.Application" package FluentValidation.DependencyInjectionExtensions
dotnet add "src/$solutionName.Application" package Microsoft.Extensions.DependencyInjection.Abstractions
dotnet add "src/$solutionName.Application" package Microsoft.Extensions.Logging.Abstractions
```

### Infrastructure

```powershell
dotnet add "src/$solutionName.Infrastructure" package Microsoft.EntityFrameworkCore
dotnet add "src/$solutionName.Infrastructure" package Microsoft.EntityFrameworkCore.Design
dotnet add "src/$solutionName.Infrastructure" package Microsoft.Extensions.Configuration.Abstractions
dotnet add "src/$solutionName.Infrastructure" package Microsoft.Extensions.DependencyInjection

# Proveedor de BD (elegir uno)
# SQL Server:
dotnet add "src/$solutionName.Infrastructure" package Microsoft.EntityFrameworkCore.SqlServer
# PostgreSQL (alternativa):
# dotnet add "src/$solutionName.Infrastructure" package Npgsql.EntityFrameworkCore.PostgreSQL
# SQLite (alternativa):
# dotnet add "src/$solutionName.Infrastructure" package Microsoft.EntityFrameworkCore.Sqlite
```

### WebApi

```powershell
dotnet add "src/$solutionName.WebApi" package Microsoft.AspNetCore.OpenApi
```

> En .NET 9+ el paquete `Microsoft.AspNetCore.OpenApi` reemplaza a Swashbuckle; usar el integrado para mantener la última versión.

### Tests (framework de pruebas)

Re-agregar el framework de pruebas a cada proyecto de test (ahora a última versión estable):

```powershell
foreach ($tp in @("$solutionName.Domain.Tests","$solutionName.Application.Tests","$solutionName.Infrastructure.Tests")) {
  dotnet add "tests/$tp" package Microsoft.NET.Test.Sdk
  dotnet add "tests/$tp" package xunit
  dotnet add "tests/$tp" package xunit.runner.visualstudio
  dotnet add "tests/$tp" package coverlet.collector
}

# Paquetes adicionales opcionales
dotnet add "tests/$solutionName.Application.Tests" package Moq
dotnet add "tests/$solutionName.Application.Tests" package FluentAssertions
dotnet add "tests/$solutionName.Infrastructure.Tests" package Microsoft.EntityFrameworkCore.InMemory
```

## Paso 6 — Configurar TFM y lenguaje a la última versión

En cada `.csproj` verificar/ajustar:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>   <!-- usar el TFM detectado -->
  <LangVersion>latest</LangVersion>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
</PropertyGroup>
```

> Reemplazar `net10.0` por el `TFM` detectado en el Paso 0. `LangVersion=latest` asegura el último C# disponible en el SDK.

## Paso 7 — Estructura de carpetas recomendada

Dentro de cada proyecto:

```
Domain/
├── Entities/
├── ValueObjects/
├── Events/
├── Exceptions/
└── Interfaces/          # IAggregateRoot, ISpecification, etc.

Application/
├── Common/
│   ├── Behaviors/       # MediatR pipeline behaviors
│   ├── Interfaces/      # IApplicationDbContext, IEmailService, ...
│   └── Mappings/
├── Features/
│   └── <Feature>/
│       ├── Commands/
│       ├── Queries/
│       ├── Validators/
│       └── DTOs/
└── DependencyInjection.cs

Infrastructure/
├── Data/
│   ├── ApplicationDbContext.cs
│   ├── Configurations/
│   └── Migrations/
├── Services/            # implementaciones de interfaces de Application
└── DependencyInjection.cs

WebApi/
├── Controllers/         # o Endpoints/ para Minimal APIs
├── Program.cs
└── appsettings.json
```

## Paso 8 — Composition root (WebApi/Program.cs)

Ejemplo mínimo para .NET 10:

```csharp
using Microsoft.AspNetCore.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Registrar capas (extension methods)
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

Crear `DependencyInjection.cs` en Application e Infrastructure con métodos de extensión `AddApplication()` / `AddInfrastructure(IConfiguration)`.

## Paso 9 — Verificación

```powershell
# Compilar toda la solución
dotnet build

# Ejecutar pruebas
dotnet test

# Verificar paquetes desactualizados (debe quedar vacío)
dotnet list package --outdated --include-transitive
```

Si `--outdated` muestra paquetes, actualizarlos con:

```powershell
dotnet add <proyecto> package <PaqueteId>
```

(sin versión, para resolver la última estable) y recompilar.

## Reglas de dependencias (verificar)

- `Domain` no referencia ningún otro proyecto ni paquete externo (solo BCL).
- `Application` referencia solo `Domain` + abstracciones de Microsoft.Extensions.
- `Infrastructure` referencia `Application` y paquetes de EF Core/proveedor.
- `WebApi` referencia `Infrastructure` (y `Application` solo si expone DTOs).
- Los tests referencian el proyecto bajo prueba, nunca al revés.

## Checklist final

- [ ] SDK detectado es el más reciente instalado.
- [ ] `global.json` (si se usa) con `rollForward: latestMajor`.
- [ ] TFM = `net{major}.0` del SDK más reciente.
- [ ] `LangVersion=latest` en todos los proyectos.
- [ ] CPM habilitado (`Directory.Packages.props`).
- [ ] Paquetes agregados sin versión fija (última estable).
- [ ] `dotnet build` sin errores.
- [ ] `dotnet test` pasa.
- [ ] `dotnet list package --outdated` sin resultados.

## Notas y troubleshooting

- Si `dotnet new webapi` genera Minimal APIs por defecto y no quieres controladores, omite `--use-controllers`. Para Clean Architecture con controladores, usa `--use-controllers`.
- Si un paquete no resuelve la última versión por caché de NuGet, ejecutar `dotnet nuget locals all --clear` y reintentar.
- Para bases de datos distintas, cambiar solo el proveedor EF Core en Infrastructure; el resto de la arquitectura no cambia.
- Para actualizar toda la solución a una nueva versión del SDK en el futuro: instalar el SDK nuevo, actualizar `global.json`, TFM y ejecutar `dotnet list package --outdated` + `dotnet add package` por cada paquete.
