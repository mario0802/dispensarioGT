# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Dispensario Médico
Backend en .NET 10 para gestionar un dispensario médico: doctores, pacientes, citas y consultas.
El desarrollo sigue **SDD (Spec-Driven Development)** con las skills `/spec` y `/spec-impl` (en `.claude/skills/`, sincronizadas con `.agents/skills/` mediante `skills-lock.json`). Los specs viven en `specs/NN-slug.md`.

> **Estado actual:** existen `Dispensario.slnx`, `global.json`, los props centrales, `BuildingBlocks` y `BuildingBlocks.Domain` (abstracciones DDD completas), los cuatro proyectos `Dispensario.*` (`Domain` solo con los VOs de `Common/`; `Api` con un `Program.cs` mínimo) y los proyectos de tests en `tests/` (features 01 a 03). Funcionan `dotnet build`, `dotnet test` y `dotnet run`; las migraciones llegan con la persistencia (feature 04 del backlog, `docs/PLAN.md`).

## Reglas de trabajo (obligatorias)
1. **No escribir código sin un spec `Approved`.** Los specs se crean con `/spec` (que no escribe código) y se implementan con `/spec-impl`. Nada de andamiaje, refactors ni features "de paso".
2. Al redactar o implementar un spec, leer las guías de `docs/` de las capas que toca: los specs deben respetar la arquitectura y las convenciones definidas ahí. Si algo es ambiguo, preguntar en vez de suponer.
3. Al terminar un spec: compilar, ejecutar los tests, verificar los criterios de aceptación y anotar el archivo del spec en el backlog de `docs/PLAN.md`. El cambio de estado (`Approved` / `Implemented`) lo hace el usuario.
4. **Idioma**: todo el código (tipos, métodos, variables, rutas, tablas, mensajes de excepción) en **inglés**. Los **comentarios** y la documentación, en **español**.
5. No hacer commits ni push salvo que el usuario lo pida.

## Stack
- .NET 10 / C# (última versión) · ASP.NET Core Minimal APIs + **Carter**
- **Clean Architecture + CQRS** con **MediatR 14** (requiere `MediatR:LicenseKey`) · FluentValidation 12 · Mapster
- **EF Core 10 + SQLite** · un solo `DispensarioDbContext`
- Autenticación con **JWT propio** (roles `Admin`, `Doctor`, `Receptionist`) y hash de contraseñas con `PasswordHasher<T>`
- OpenAPI nativo + **Scalar** · tests con xUnit v3, Shouldly, NSubstitute y WebApplicationFactory

## Estructura
```
Dispensario.slnx  Directory.Build.props  Directory.Packages.props   # se crean en el spec 01
.claude/skills/  .agents/skills/       # skills SDD: spec, spec-impl
specs/                                 # specs generados por /spec (NN-slug.md) + .spec-config.yml
frontend/                              # se define más adelante
docs/                                  # plan/backlog, arquitectura y guías
src/
├─ BuildingBlocks/
│  ├─ BuildingBlocks/                  # código común: CQRS, behaviors, excepciones, paginación
│  └─ BuildingBlocks.Domain/           # abstracciones DDD puras: Entity, AggregateRoot, ValueObject, IDomainEvent, IAuditable, DomainException
└─ Services/Dispensario/
   ├─ Dispensario.Domain/              # aggregates, value objects, domain events (solo depende de BuildingBlocks.Domain)
   ├─ Dispensario.Application/         # commands/queries/handlers/validators por módulo
   ├─ Dispensario.Infrastructure/      # EF Core/SQLite, interceptores, JWT, seed
   └─ Dispensario.Api/                 # endpoints Carter, auth, OpenAPI, Program.cs
tests/                                 # todos los proyectos de tests, sin subcarpetas
├─ BuildingBlocks.UnitTests/           # tests de BuildingBlocks y BuildingBlocks.Domain (CustomExceptionHandler, Entity…)
├─ Dispensario.Domain.UnitTests/
├─ Dispensario.Application.UnitTests/
└─ Dispensario.Api.IntegrationTests/
```
Dentro de cada capa el código se agrupa **por módulo**: `Users/`, `Doctors/`, `Patients/`, `Appointments/`, `Consultations/`.

## Reglas de arquitectura (resumen)
- Dependencias: `Api → Application, Infrastructure` · `Infrastructure → Application` · `Application → Domain, BuildingBlocks` · `Domain → BuildingBlocks.Domain` · `BuildingBlocks → BuildingBlocks.Domain` · `BuildingBlocks.Domain →` solo `MediatR.Contracts`.
- Los módulos **solo se relacionan por ID** (`PatientId`, `DoctorId`…): nada de navegaciones de EF entre módulos, y un handler nunca modifica un aggregate de otro módulo. Entre módulos se reacciona con domain events.
- Para los commands, queries y handlers se usan `ICommand`, `IQuery`, `ICommandHandler` e `IQueryHandler` de `BuildingBlocks.CQRS`.
- Los errores se señalan con las excepciones de `BuildingBlocks.Exceptions` (NotFound, Conflict, BadRequest, Forbidden…) y con `DomainException` (`BuildingBlocks.Domain`) para las reglas de negocio. `CustomExceptionHandler` las convierte en ProblemDetails, así que no se devuelven códigos de error a mano desde los endpoints.
- Fechas en UTC con `TimeProvider`; nunca `DateTime.Now`.

## Detalles no obvios
- `ValidationBehavior` (BuildingBlocks) solo se aplica a `ICommand<T>`: las queries **no** pasan por FluentValidation.
- `CustomExceptionHandler` mapea `DomainException` → 400, `UnauthorizedException` → 401 y `ForbiddenException` → 403; cualquier excepción no mapeada cae en 500.
- Los tests usan `xunit.v3.mtp-off` (xUnit v3 sin Microsoft.Testing.Platform) para ejecutarse con VSTest: `xunit.v3` 4.x trae MTP 2.x, que en el SDK de .NET 10 bloquea `dotnet test` con VSTest.
- MediatR 14 es de licencia comercial: sin `MediatR:LicenseKey` registra un aviso de licencia.
- SQLite no admite esquemas (los módulos se separan por nombre de tabla y carpeta de configuración) y no ordena ni compara bien `DateTimeOffset`: se usa `DateTime` UTC con el sufijo `Utc`.
- Los tests usan SQLite `:memory:`, nunca el proveedor EF InMemory.

Detalle completo en:
- `docs/ARCHITECTURE.md`: especificación de la arquitectura
- `docs/PLAN.md`: backlog de features, flujo SDD y preguntas abiertas
- `docs/guides/CONVENTIONS.md`: nombres, estilo y comentarios
- `docs/guides/DOMAIN.md`: modelo de dominio y reglas DDD
- `docs/guides/APPLICATION.md`: CQRS, validación y mapeo
- `docs/guides/INFRASTRUCTURE.md`: EF Core/SQLite, migraciones, auth y seed
- `docs/guides/API.md`: endpoints Carter, rutas, políticas y OpenAPI
- `docs/guides/TESTING.md`: estrategia de tests

## Comandos
```bash
dotnet build Dispensario.slnx
dotnet test Dispensario.slnx
dotnet test tests/Dispensario.Domain.UnitTests --filter "FullyQualifiedName~AppointmentTests"   # una clase o un test
dotnet run --project src/Services/Dispensario/Dispensario.Api            # Scalar en /scalar, health en /health

# Migraciones (requiere la herramienta dotnet-ef 10.x)
dotnet ef migrations add <Name> -p src/Services/Dispensario/Dispensario.Infrastructure -s src/Services/Dispensario/Dispensario.Api -o Data/Migrations
dotnet ef database update      -p src/Services/Dispensario/Dispensario.Infrastructure -s src/Services/Dispensario/Dispensario.Api

# Secretos locales (nunca en appsettings versionado)
dotnet user-secrets set "Jwt:Key" "<min-32-chars>" --project src/Services/Dispensario/Dispensario.Api
dotnet user-secrets set "MediatR:LicenseKey" "<key>" --project src/Services/Dispensario/Dispensario.Api
```
