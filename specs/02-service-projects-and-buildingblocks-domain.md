# SPEC 02 — Proyectos del servicio y BuildingBlocks.Domain

> **Status:** Implemented
> **Depends on:** SPEC 01
> **Date:** 2026-10-26
> **Objective:** Crear `BuildingBlocks.Domain` con `DomainException`, los proyectos `Dispensario.*` y de tests con sus referencias, y corregir `CustomExceptionHandler` y `LoggingBehavior` según ARCHITECTURE §5.

## Por qué este spec existe

La fila 03 del backlog trae todas las abstracciones DDD (`Entity`, `AggregateRoot`, `ValueObject`, `IDomainEvent`, `IAuditable`, `DomainException`).
Pero el mapeo `DomainException → 400` de `CustomExceptionHandler` necesita que `DomainException` ya exista.
Por eso aquí se crea `BuildingBlocks.Domain` solo con `DomainException`, y el resto de las abstracciones se queda en la fila 03.
Además se crea `BuildingBlocks.UnitTests`, que no figura en la estructura de `CLAUDE.md`, para probar los mapeos nuevos del handler.

## Scope

**In:**

- `.gitignore`: añadir `*.db`, `*.db-shm` y `*.db-wal`.
- Proyecto `src/BuildingBlocks/BuildingBlocks.Domain` (classlib) con el paquete `MediatR.Contracts` y la clase `DomainException`.
- Referencia `BuildingBlocks → BuildingBlocks.Domain`.
- Nueva `ForbiddenException` en `BuildingBlocks/Exceptions`, con la misma forma que `UnauthorizedException`.
- `CustomExceptionHandler`: mapear `DomainException` → 400, `UnauthorizedException` → 401 y `ForbiddenException` → 403, y quitar el `DateTime.UtcNow` del log.
- `LoggingBehavior`: `timeTaken.Seconds` → `timeTaken.TotalSeconds` (en la condición y en el mensaje).
- Proyecto `src/BuildingBlocks/tests/BuildingBlocks.UnitTests` con tests de los tres mapeos nuevos.
- Proyectos `Dispensario.Domain`, `Dispensario.Application` e `Dispensario.Infrastructure` (classlib) y `Dispensario.Api` (web) en `src/Services/Dispensario/`, solo con referencias entre proyectos.
- `Program.cs` mínimo en `Dispensario.Api` (CreateBuilder → Build → Run, sin endpoints).
- Proyectos `Dispensario.Domain.UnitTests`, `Dispensario.Application.UnitTests` y `Dispensario.Api.IntegrationTests` en `src/Services/Dispensario/tests/`, con sus referencias y paquetes de test, sin tests.
- Versiones de los paquetes nuevos en `Directory.Packages.props` y todos los proyectos nuevos en `Dispensario.slnx`.
- Actualizar `docs/PLAN.md`, `docs/ARCHITECTURE.md` §5 y `CLAUDE.md` para que reflejen el estado nuevo.

**Out of scope (for future specs):**

- `Entity<TId>`, `AggregateRoot<TId>`, `ValueObject`, `IDomainEvent` e `IAuditable` (fila 03).
- Paquetes de EF Core, SQLite, Carter, Scalar y `Microsoft.Extensions.TimeProvider.Testing` en los proyectos del servicio (filas 04 y 05).
- `AddApplication`, `AddInfrastructure`, registro de `CustomExceptionHandler` y cualquier configuración del host (fila 05).
- Refactor de `LoggingBehavior` para medir con `TimeProvider` y test automático del umbral de 3 s.
- Otros cambios en `CustomExceptionHandler` (p. ej. no exponer el mensaje de las excepciones no controladas en el 500).
- Tests en los tres proyectos de tests del servicio.
- `TreatWarningsAsErrors` y Microsoft.Testing.Platform.

## Data model

Este spec no introduce estructuras del dominio del dispensario. Introduce estos tipos y proyectos (contenido orientativo).

```csharp
// src/BuildingBlocks/BuildingBlocks.Domain/DomainException.cs
namespace BuildingBlocks.Domain;

// Se lanza cuando un aggregate rompe una regla de negocio; CustomExceptionHandler la traduce a 400
public class DomainException(string message) : Exception(message);
```

```csharp
// src/BuildingBlocks/BuildingBlocks/Exceptions/ForbiddenException.cs
namespace BuildingBlocks.Exceptions;

public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
    public ForbiddenException(string message, string details) : base(message) => Details = details;
    public string? Details { get; }
}
```

Mapeo final de `CustomExceptionHandler` (el `Title` sigue siendo el nombre del tipo de la excepción):

| Excepción | Estado |
| --- | --- |
| `InternalServerException` | 500 |
| `ValidationException` | 400 |
| `BadRequestException` | 400 |
| `DomainException` | 400 |
| `UnauthorizedException` | 401 |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| cualquier otra | 500 |

Proyectos y referencias:

| Proyecto | Ruta | Tipo | Referencias | Paquetes |
| --- | --- | --- | --- | --- |
| `BuildingBlocks.Domain` | `src/BuildingBlocks/BuildingBlocks.Domain` | classlib | — | `MediatR.Contracts` |
| `BuildingBlocks` | `src/BuildingBlocks/BuildingBlocks` | classlib | `BuildingBlocks.Domain` | (los actuales) |
| `BuildingBlocks.UnitTests` | `src/BuildingBlocks/tests/BuildingBlocks.UnitTests` | xUnit v3 | `BuildingBlocks` | test |
| `Dispensario.Domain` | `src/Services/Dispensario/Dispensario.Domain` | classlib | `BuildingBlocks.Domain` | — |
| `Dispensario.Application` | `src/Services/Dispensario/Dispensario.Application` | classlib | `Dispensario.Domain`, `BuildingBlocks` | — |
| `Dispensario.Infrastructure` | `src/Services/Dispensario/Dispensario.Infrastructure` | classlib | `Dispensario.Application` | — |
| `Dispensario.Api` | `src/Services/Dispensario/Dispensario.Api` | web | `Dispensario.Application`, `Dispensario.Infrastructure` | — |
| `Dispensario.Domain.UnitTests` | `src/Services/Dispensario/tests/Dispensario.Domain.UnitTests` | xUnit v3 | `Dispensario.Domain` | test |
| `Dispensario.Application.UnitTests` | `src/Services/Dispensario/tests/Dispensario.Application.UnitTests` | xUnit v3 | `Dispensario.Application`, `Dispensario.Infrastructure` | test |
| `Dispensario.Api.IntegrationTests` | `src/Services/Dispensario/tests/Dispensario.Api.IntegrationTests` | xUnit v3 | `Dispensario.Api` | test + `Microsoft.AspNetCore.Mvc.Testing` |

Paquetes "test": `xunit.v3.mtp-off`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Shouldly` y `NSubstitute`.

```xml
<!-- Directory.Packages.props: entradas nuevas (versiones = última estable al implementar) -->
<PackageVersion Include="MediatR.Contracts" Version="x.y.z" />
<PackageVersion Include="xunit.v3.mtp-off" Version="x.y.z" />
<PackageVersion Include="xunit.runner.visualstudio" Version="x.y.z" />
<PackageVersion Include="Microsoft.NET.Test.Sdk" Version="x.y.z" />
<PackageVersion Include="Shouldly" Version="x.y.z" />
<PackageVersion Include="NSubstitute" Version="x.y.z" />
<PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.x" />
```

```xml
<!-- Forma de un proyecto de tests (xUnit v3 necesita OutputType Exe) -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.v3.mtp-off" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="NSubstitute" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="Shouldly" />
  </ItemGroup>
</Project>
```

```csharp
// src/Services/Dispensario/Dispensario.Api/Program.cs
// Host mínimo: la configuración real (DI, Carter, OpenAPI, auth) llega en la fila 05
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.Run();
```

Carpetas de `Dispensario.slnx` (reflejan el disco):

- `/src/BuildingBlocks/`: `BuildingBlocks`, `BuildingBlocks.Domain`.
- `/src/BuildingBlocks/tests/`: `BuildingBlocks.UnitTests`.
- `/src/Services/Dispensario/`: los cuatro `Dispensario.*`.
- `/src/Services/Dispensario/tests/`: los tres proyectos de tests del servicio.

Convenciones:

- Ningún `.csproj` nuevo lleva `TargetFramework`, `Nullable`, `ImplicitUsings`, `LangVersion` ni atributos `Version`: vienen de los props de la raíz.
- Se borran los archivos de ejemplo de las plantillas (`Class1.cs`, `UnitTest1.cs`, el `GET /` "Hello World").

## Implementation plan

1. Añadir `*.db`, `*.db-shm` y `*.db-wal` a `.gitignore`. Verificar: `git check-ignore dispensario.db` devuelve la ruta.
2. Crear `BuildingBlocks.Domain` (classlib, sin `Class1.cs`) con `MediatR.Contracts` y `DomainException.cs`; añadir la versión a `Directory.Packages.props`, la referencia desde `BuildingBlocks.csproj` y el proyecto a `/src/BuildingBlocks/` en `Dispensario.slnx`. Verificar: `dotnet build Dispensario.slnx` compila.
3. Crear `ForbiddenException`. En `CustomExceptionHandler`, añadir los casos `DomainException` (400), `UnauthorizedException` (401) y `ForbiddenException` (403), y quitar el parámetro `{time}` y el `DateTime.UtcNow` del `LogError`. Verificar: compila.
4. En `LoggingBehavior`, cambiar `timeTaken.Seconds` por `timeTaken.TotalSeconds` en la condición y en el mensaje de aviso. Verificar: compila.
5. Añadir a `Directory.Packages.props` las versiones de los paquetes de test. Crear `BuildingBlocks.UnitTests` con referencia a `BuildingBlocks` y la clase `CustomExceptionHandlerTests`. Esta clase usa `DefaultHttpContext` con un `MemoryStream` como body y `NullLogger`, y comprueba el status y el `ProblemDetails.Status` para `DomainException`, `UnauthorizedException` y `ForbiddenException`. Añadir el proyecto a `/src/BuildingBlocks/tests/`. Verificar: `dotnet test Dispensario.slnx` pasa los tres tests.
6. Crear `Dispensario.Domain`, `Dispensario.Application` e `Dispensario.Infrastructure` (classlib, sin `Class1.cs`) con sus referencias y añadirlos a `/src/Services/Dispensario/`. Verificar: compila.
7. Crear `Dispensario.Api` (web) con referencias a Application e Infrastructure y el `Program.cs` mínimo; quitar del `.csproj` las propiedades heredadas de los props. Se conservan el `appsettings.json`, el `appsettings.Development.json` y el `Properties/launchSettings.json` de la plantilla. Añadirlo a la solución. Verificar: compila y `dotnet run --project src/Services/Dispensario/Dispensario.Api` arranca sin errores.
8. Crear los tres proyectos de tests del servicio (sin `UnitTest1.cs`) con sus referencias y paquetes; `Dispensario.Api.IntegrationTests` lleva además `Microsoft.AspNetCore.Mvc.Testing`. Añadirlos a `/src/Services/Dispensario/tests/`. Verificar: `dotnet test Dispensario.slnx` termina con éxito.
9. Actualizar la documentación:
   - `docs/PLAN.md`: la fila 02 apunta a este spec y en la fila 03 se indica que `DomainException` ya existe.
   - `docs/ARCHITECTURE.md` §5: los "Ajustes pendientes" pasan a ser ajustes hechos en la feature 02, y `ForbiddenException` entra en la fila de excepciones.
   - `CLAUDE.md`: actualizar "Estado actual", el árbol de "Estructura" (con `BuildingBlocks.UnitTests`) y la línea de `CustomExceptionHandler` en "Detalles no obvios".

## Acceptance criteria

- [ ] `.gitignore` contiene `*.db`, `*.db-shm` y `*.db-wal`.
- [ ] `dotnet build Dispensario.slnx` termina sin errores y sin warnings nuevos respecto a SPEC 01.
- [ ] `dotnet sln Dispensario.slnx list` muestra los 10 proyectos de la tabla del data model.
- [ ] Las referencias de cada `.csproj` coinciden exactamente con las de la tabla del data model.
- [ ] `BuildingBlocks.Domain.csproj` solo tiene el paquete `MediatR.Contracts` y ninguna referencia a proyectos ni `FrameworkReference`.
- [ ] Ningún `.csproj` nuevo contiene `TargetFramework`, `Nullable`, `ImplicitUsings`, `LangVersion` ni atributos `Version`.
- [ ] Los proyectos `Dispensario.Domain`, `Dispensario.Application`, `Dispensario.Infrastructure` y `Dispensario.Api` no tienen `PackageReference`.
- [ ] No queda ningún `Class1.cs` ni `UnitTest1.cs` en el repositorio.
- [ ] Existe `BuildingBlocks.Domain.DomainException`.
- [ ] Existe `BuildingBlocks.Exceptions.ForbiddenException`.
- [ ] `CustomExceptionHandler` responde 400 a `DomainException`, 401 a `UnauthorizedException` y 403 a `ForbiddenException` (cubierto por `CustomExceptionHandlerTests`).
- [ ] `CustomExceptionHandler.cs` no contiene `DateTime`.
- [ ] `LoggingBehavior.cs` usa `TotalSeconds` y no contiene `.Seconds`.
- [ ] `dotnet test Dispensario.slnx` termina con éxito y ejecuta los tres tests de `BuildingBlocks.UnitTests`.
- [ ] `dotnet run --project src/Services/Dispensario/Dispensario.Api` arranca y escucha sin excepciones.
- [ ] `docs/PLAN.md` fila 02 apunta a `specs/02-service-projects-and-buildingblocks-domain.md`.
- [ ] `docs/ARCHITECTURE.md` §5 y `CLAUDE.md` ya no listan como pendientes los ajustes de `CustomExceptionHandler` y `LoggingBehavior`.

## Decisions

- **Yes:** `BuildingBlocks.Domain` solo con `DomainException`. Es lo único que necesita el handler. El resto de las abstracciones va con sus tests en la fila 03.
- **No:** traer aquí todas las abstracciones DDD. Mezclaría la estructura de proyectos con el diseño del dominio.
- **Yes:** `MediatR.Contracts` ya en `BuildingBlocks.Domain`, como fija ARCHITECTURE §2, aunque hasta la fila 03 (`IDomainEvent : INotification`) no se use.
- **Yes:** paquetes mínimos en los proyectos del servicio. EF Core, Carter, Scalar, etc. se añaden en el spec que los usa.
- **No:** declarar ya todos los paquetes del stack. Serían dependencias muertas y versiones que se decidirían sin contexto.
- **Yes:** nuevo proyecto `BuildingBlocks.UnitTests` para probar los mapeos. TESTING.md exige un test por criterio y el handler es código de BuildingBlocks, no del servicio.
- **No:** smoke tests vacíos en los proyectos de tests del servicio. No aportan nada que no compruebe ya `dotnet test`.
- **Yes:** VSTest (`Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio`). Funciona con `dotnet test` y Visual Studio sin tocar `global.json`.
- **No:** Microsoft.Testing.Platform. Requiere configurar `global.json` y cambia los filtros. Se puede revisar más adelante.
- **Yes (decidido al implementar):** `xunit.v3.mtp-off` en lugar de `xunit.v3`. `xunit.v3` 4.x trae Microsoft.Testing.Platform 2.x, que en el SDK de .NET 10 rompe `dotnet test` con VSTest. La variante `mtp-off` es la misma versión de xUnit sin MTP.
- **No:** bajar a `xunit.v3` 3.2.x. Funcionaría con VSTest, pero no es la última versión.
- **Yes:** `Program.cs` mínimo sin endpoints. En .NET 10, `Program` ya es público con top-level statements, así que no hace falta `public partial class Program`.
- **Yes:** quitar el timestamp del `LogError` de `CustomExceptionHandler`. El proveedor de logging ya registra la hora y así se cumple la regla de no usar `DateTime.UtcNow`.
- **No:** inyectar `TimeProvider` en `CustomExceptionHandler`. Añadiría una dependencia solo para un dato que el log ya tiene.
- **Yes:** `LoggingBehavior` sin test automático. Es un cambio de una línea y se verifica con un criterio sobre el código.
- **No:** refactorizar `LoggingBehavior` con `TimeProvider`, ni un test con espera real de más de 3 s.
- **Yes:** `ForbiddenException` con la misma forma que las demás excepciones de `BuildingBlocks` (constructor con `message` y con `message` + `details`).
- **Yes:** conservar `appsettings*.json` y `launchSettings.json` de la plantilla web. La fila 05 los completa.

## Risks

| Risk | Mitigation |
| --- | --- |
| La versión de `MediatR.Contracts` choca con la que exige `MediatR` 14.2.0 (error NU1605/NU1109 con CPM) | Se usa la versión de `MediatR.Contracts` de la que depende `MediatR` 14.2.0 o una superior compatible. |
| `dotnet test` con proyectos de tests sin tests devuelve aviso o código de salida distinto de 0 | Se comprueba en el paso 8. Si falla, se anota y se decide con el usuario antes de añadir nada. |
| Las plantillas de .NET 10 generan propiedades que duplican los props (`TargetFramework`, `Nullable`…) | Se borran de cada `.csproj` y se comprueba con el criterio de aceptación correspondiente. |
| xUnit v3 necesita `OutputType Exe` y puede dar error si falta | Se incluye en todos los `.csproj` de tests. |

## What is **not** in this spec

- `Entity`, `AggregateRoot`, `ValueObject`, `IDomainEvent` e `IAuditable`.
- Paquetes de EF Core, SQLite, Carter, Scalar o TimeProvider.Testing en el servicio.
- Configuración del host, registro de DI o de `CustomExceptionHandler`.
- Tests en los proyectos de tests del servicio.
- Refactor de `LoggingBehavior` con `TimeProvider`.
- `TreatWarningsAsErrors` y Microsoft.Testing.Platform.

Cada uno de ellos va en su propio spec (la mayoría en las filas 03–05 del backlog).
