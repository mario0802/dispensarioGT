# SPEC 01 — Solución y BuildingBlocks en .NET 10

> **Status:** Implemented
> **Depends on:** —
> **Date:** 2026-10-26/spe
> **Objective:** Crear `Dispensario.slnx` con la configuración central de build y paquetes, y migrar `BuildingBlocks` a `net10.0` dentro de esa solución.

## Por qué este spec existe

La fila 01 del backlog (`docs/PLAN.md`) mezcla la infraestructura de build con los proyectos del servicio, `BuildingBlocks.Domain` y correcciones de código.
Se separa en dos specs para que el primero sea pequeño y verificable: aquí solo se deja una solución que compila con `dotnet build Dispensario.slnx`.
El resto pasa a una nueva fila 02 del backlog.

## Scope

**In:**

- `global.json` en la raíz que fija el SDK 10.
- `Directory.Build.props` en la raíz con `TargetFramework`, `Nullable`, `ImplicitUsings` y `LangVersion`.
- `Directory.Packages.props` en la raíz con Central Package Management y las versiones de los paquetes de `BuildingBlocks`.
- `Dispensario.slnx` en la raíz con `BuildingBlocks.csproj` y los archivos de configuración como solution items.
- `BuildingBlocks.csproj` a `net10.0` (heredado del props) y con los ajustes de paquetes de `docs/ARCHITECTURE.md` §5.
- Actualizar el backlog de `docs/PLAN.md`: nueva fila 02 con el resto de la antigua fila 01 y renumeración de las siguientes.
- Actualizar las referencias a "feature 01" en `CLAUDE.md` y `docs/ARCHITECTURE.md` §5 para que apunten a la numeración nueva.

**Out of scope (for future specs):**

- Proyectos `Dispensario.Domain`, `Dispensario.Application`, `Dispensario.Infrastructure` y `Dispensario.Api`.
- Proyectos de tests (`Domain.UnitTests`, `Application.UnitTests`, `Api.IntegrationTests`).
- `src/BuildingBlocks/BuildingBlocks.Domain` y la referencia desde `BuildingBlocks`.
- Cambios de código en `BuildingBlocks`: mapeos de `CustomExceptionHandler` (`UnauthorizedException` → 401, `ForbiddenException` → 403, `DomainException` → 400) y `TotalSeconds` en `LoggingBehavior`.
- Añadir `*.db`, `*.db-shm` y `*.db-wal` a `.gitignore`.
- `TreatWarningsAsErrors`.
- Actualizar FluentValidation, Mapster o MediatR a versiones más nuevas.

## Data model

Este spec no introduce estructuras de datos del dominio. Introduce estos archivos de configuración (contenido orientativo, no definitivo):

```json
// global.json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

```xml
<!-- Directory.Build.props -->
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

```xml
<!-- Directory.Packages.props -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="FluentValidation" Version="12.1.1" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />
    <PackageVersion Include="Mapster" Version="10.0.12" />
    <PackageVersion Include="MediatR" Version="14.2.0" />
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.x" />
  </ItemGroup>
</Project>
```

`10.0.x` se sustituye al implementar por la última versión estable 10.0.x de NuGet.

```xml
<!-- Dispensario.slnx -->
<Solution>
  <Folder Name="/Solution Items/">
    <File Path="Directory.Build.props" />
    <File Path="Directory.Packages.props" />
    <File Path="global.json" />
  </Folder>
  <Folder Name="/src/" />
  <Folder Name="/src/BuildingBlocks/">
    <Project Path="src/BuildingBlocks/BuildingBlocks/BuildingBlocks.csproj" />
  </Folder>
</Solution>
```

```xml
<!-- src/BuildingBlocks/BuildingBlocks/BuildingBlocks.csproj (resultado) -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
    <PackageReference Include="Mapster" />
    <PackageReference Include="MediatR" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
  </ItemGroup>
</Project>
```

Convenciones:

- Las versiones de paquetes solo viven en `Directory.Packages.props`; ningún `.csproj` lleva `Version`.
- Las carpetas de solución reflejan las carpetas de disco (`/src/BuildingBlocks/`); los futuros proyectos irán en `/src/Services/Dispensario/`.

## Implementation plan

1. Crear `global.json` en la raíz con el SDK `10.0.100` y `rollForward: latestFeature`. Verificar: `dotnet --version` en la raíz devuelve una versión 10.0.x.
2. Crear `Directory.Build.props` en la raíz y quitar del `BuildingBlocks.csproj` el `PropertyGroup` con `TargetFramework`, `Nullable` e `ImplicitUsings`. Verificar: `dotnet build src/BuildingBlocks/BuildingBlocks` compila para `net10.0`.
3. Crear `Directory.Packages.props` con CPM y las versiones; quitar los atributos `Version` del `.csproj`; subir JwtBearer a la última 10.0.x estable. Verificar: `dotnet build src/BuildingBlocks/BuildingBlocks` compila.
4. En `BuildingBlocks.csproj`, añadir `<FrameworkReference Include="Microsoft.AspNetCore.App" />` y quitar `StackExchange.Redis` y `Microsoft.FeatureManagement.AspNetCore` (también de `Directory.Packages.props`). Verificar: compila sin errores.
5. Crear `Dispensario.slnx` (`dotnet new sln --format slnx -n Dispensario`), añadir `BuildingBlocks.csproj` en la carpeta `/src/BuildingBlocks/` y los solution items. Verificar: `dotnet build Dispensario.slnx` compila.
6. Actualizar `docs/PLAN.md`: la fila 01 queda con el contenido de este spec y su archivo en la columna **Spec**; se crea la fila 02 "Proyectos del servicio y BuildingBlocks.Domain" con el resto de la antigua fila 01; las filas 02–17 pasan a 03–18. Actualizar las menciones a "feature 01" de `CLAUDE.md` y `docs/ARCHITECTURE.md` §5 según la nueva numeración.

## Acceptance criteria

- [x] Existen `global.json`, `Directory.Build.props`, `Directory.Packages.props` y `Dispensario.slnx` en la raíz del repositorio.
- [x] `dotnet --version` ejecutado en la raíz devuelve una versión 10.0.x.
- [x] `dotnet build Dispensario.slnx` termina sin errores.
- [x] La salida del build está en `bin/Debug/net10.0/` de `BuildingBlocks`.
- [x] `BuildingBlocks.csproj` no contiene `TargetFramework`, `Nullable`, `ImplicitUsings` ni ningún atributo `Version`.
- [x] `BuildingBlocks.csproj` contiene `<FrameworkReference Include="Microsoft.AspNetCore.App" />`.
- [x] Ni `BuildingBlocks.csproj` ni `Directory.Packages.props` mencionan `StackExchange.Redis` ni `Microsoft.FeatureManagement.AspNetCore`.
- [x] `Directory.Packages.props` tiene `ManagePackageVersionsCentrally` en `true` y JwtBearer en una versión 10.0.x.
- [x] `dotnet sln Dispensario.slnx list` muestra `BuildingBlocks.csproj`.
- [x] Ningún archivo `.cs` de `BuildingBlocks` ha cambiado.
- [x] `docs/PLAN.md` tiene la nueva fila 02 y las demás renumeradas, y la fila 01 apunta a `specs/01-solution-and-buildingblocks-net10.md`.

## Decisions

- **Yes:** separar la fila 01 del backlog en dos specs. Este queda limitado a build y paquetes, y es verificable con un solo `dotnet build`.
- **No:** crear ahora los proyectos `Dispensario.*` y de tests vacíos. Van en la fila 02, junto a `BuildingBlocks.Domain`.
- **Yes:** aplicar los ajustes de paquetes de ARCHITECTURE §5 (FrameworkReference, quitar Redis y FeatureManagement). Ningún `.cs` usa esos paquetes, así que no rompe nada.
- **No:** corregir `CustomExceptionHandler` y `LoggingBehavior` aquí. Se hacen junto a `BuildingBlocks.Domain` porque el mapeo de `DomainException` lo necesita.
- **Yes:** `TargetFramework` en `Directory.Build.props`. Todos los proyectos del repo apuntarán a `net10.0`.
- **Yes:** `global.json` con `rollForward: latestFeature`. En la máquina hay SDK 8, 9 y 10; así se usa siempre el 10 sin atarse a un parche concreto.
- **No:** `TreatWarningsAsErrors`. Se puede valorar en otro spec.
- **No:** `*.db` en `.gitignore` en este spec. No hay base de datos todavía; va en la fila 02.
- **Yes:** mantener FluentValidation 12.1.1, Mapster 10.0.12 y MediatR 14.2.0; solo JwtBearer sube a 10.0.x porque la 8.0.30 es de .NET 8.
- **Yes:** carpetas de solución que reflejan el disco. Mantiene la misma organización cuando lleguen los proyectos del servicio.

## Risks

| Risk | Mitigation |
| --- | --- |
| Alguna versión de FluentValidation, Mapster o MediatR no restaura en net10 | Si falla el restore, se sube ese paquete a la versión estable mínima compatible y se anota en el spec. |
| Tras la migración aparecen warnings nuevos (nullable, obsoletos) | No bloquean el build; se anotan y se corrigen en el spec de la fila 02 si tocan código. |
| `dotnet new sln` genera una solución con un formato distinto a `.slnx` | Se usa `--format slnx` de forma explícita. |

## What is **not** in this spec

- Proyectos `Dispensario.*` y proyectos de tests.
- `BuildingBlocks.Domain`.
- Cambios en `CustomExceptionHandler`, `LoggingBehavior` o cualquier otro `.cs`.
- Entradas `*.db` en `.gitignore`.
- `TreatWarningsAsErrors`.

Cada uno de ellos va en su propio spec (la mayoría en la nueva fila 02 del backlog).
