# Especificación de Arquitectura — Dispensario

## 1. Objetivos
- Backend mantenible, con **Clean Architecture + CQRS**.
- **Monolito modular** hoy, que se pueda partir en **microservicios** mañana sin reescribir el dominio.
- Persistencia simple con **EF Core + SQLite** (un solo archivo `.db`), sustituible por otro proveedor más adelante.
- API REST documentada (OpenAPI + Scalar) y protegida con JWT.

## 2. Vista general de capas

```
            ┌──────────────────────────────────────────┐
            │              Dispensario.Api             │  Carter endpoints, auth, OpenAPI, DI raíz
            └──────────────┬─────────────────┬─────────┘
                           │                 │
                           ▼                 ▼
┌────────────────────────────────┐   ┌──────────────────────────────┐
│     Dispensario.Application    │◄──│  Dispensario.Infrastructure  │  EF Core/SQLite, JWT, hasher, seed
│ Commands/Queries/Handlers/DTOs │   └──────────────────────────────┘
└───────┬────────────────┬───────┘
        ▼                ▼
┌──────────────────┐  ┌──────────────────────────────┐
│Dispensario.Domain│  │ BuildingBlocks (compartido)  │  CQRS, behaviors, excepciones, paginación
└────────┬─────────┘  └──────────────┬───────────────┘
         ▼                           ▼
┌──────────────────────────────────────────────────┐
│     BuildingBlocks.Domain (compartido, puro)     │  Entity, AggregateRoot, ValueObject, IDomainEvent, DomainException
└──────────────────────────────────────────────────┘
```

| Proyecto | Tipo | Referencia a | Contenido | Prohibido |
|---|---|---|---|---|
| `Dispensario.Domain` | classlib | `BuildingBlocks.Domain` | Aggregates, entidades, value objects, IDs tipados, domain events y enums del dispensario | EF Core, ASP.NET, FluentValidation, DTOs |
| `Dispensario.Application` | classlib | Domain, BuildingBlocks, `Microsoft.EntityFrameworkCore` (solo abstracciones `DbSet`) | Commands, queries, handlers, validators, DTOs, event handlers, interfaces (`IApplicationDbContext`, `ICurrentUser`…) | Implementaciones concretas (SQLite, JWT), `HttpContext` |
| `Dispensario.Infrastructure` | classlib | Application | `DispensarioDbContext`, configuraciones EF, interceptores, migraciones, JWT, password hasher, seed | Lógica de negocio |
| `Dispensario.Api` | web | Application, Infrastructure | Endpoints Carter, request/response, auth/policies, OpenAPI/Scalar, health checks, `Program.cs` | Lógica de negocio, acceso directo al DbContext |
| `BuildingBlocks` | classlib | BuildingBlocks.Domain, MediatR, FluentValidation, Mapster, ASP.NET (framework ref) | Código **genérico** reutilizable por cualquier servicio futuro | Cualquier cosa específica del dispensario |
| `BuildingBlocks.Domain` | classlib | `MediatR.Contracts` | Abstracciones DDD genéricas: `Entity<TId>`, `AggregateRoot<TId>`, `ValueObject`, `IDomainEvent`, `IAuditable`, `DomainException` | ASP.NET, EF Core y cualquier otra dependencia de infraestructura |

> **Decisión:** `Application` usa `IApplicationDbContext` (con `DbSet<T>`) en lugar de repositorios genéricos. Así no se duplica lo que EF ya ofrece (unit of work y repositorio). El precio es depender de `Microsoft.EntityFrameworkCore` en Application, algo que se acepta de forma consciente.

## 3. Organización por módulos (vertical slices dentro de cada capa)
Módulos v1: `Users`, `Doctors`, `Patients`, `Appointments`, `Consultations`.

```
Dispensario.Domain/
  Common/  Users/  Doctors/  Patients/  Appointments/  Consultations/
Dispensario.Application/
  Abstractions/  Common/  Users/  Doctors/  Patients/  Appointments/  Consultations/
    <Module>/Commands/<Feature>/   <Module>/Queries/<Feature>/   <Module>/Dtos/   <Module>/EventHandlers/
Dispensario.Infrastructure/
  Data/Configurations/<Module>/  Data/Interceptors/  Data/Migrations/  Data/Seed/  Authentication/
Dispensario.Api/
  Endpoints/<Module>/<Feature>Endpoint.cs   Extensions/   Program.cs
```

### Reglas de aislamiento entre módulos
1. **Referencia solo por ID tipado.** `Appointment` guarda un `PatientId` y un `DoctorId`, nunca `Patient Patient`.
2. **Sin FKs de navegación entre módulos** en EF. Se permite la FK física en SQLite para la integridad, pero sin propiedad de navegación.
3. **Un handler escribe en un único módulo.** Puede *leer* otros (p. ej. comprobar que el doctor existe y está activo), pero no modificarlos.
4. **La comunicación entre módulos que cambia estado se hace con domain events**. Ejemplo: `ConsultationClosedDomainEvent` → el handler en `Appointments/EventHandlers` completa la cita.
5. `Domain/Common` contiene solo value objects genéricos (`FullName`, `Email`, `PhoneNumber`, `Address`) que se copiarían a cada microservicio.

### Ruta de extracción a microservicios
| Hoy (monolito modular) | Mañana (microservicio) |
|---|---|
| Carpeta `<Module>/` en cada capa | Proyectos `<Module>.Domain/.Application/.Infrastructure/.Api` propios |
| Tablas del módulo en `dispensario.db` | Base de datos propia del servicio |
| Lectura cruzada vía `IApplicationDbContext` | Llamada HTTP/gRPC o réplica local alimentada por eventos |
| Domain event en proceso (MediatR `INotification`) | Integration event por un bus (RabbitMQ/MassTransit), con *outbox* |
| JWT emitido por el módulo Users | Servicio de identidad; los demás solo validan el token |

`BuildingBlocks` se convierte entonces en un paquete compartido (o en varios: `BuildingBlocks`, `BuildingBlocks.Messaging`).

## 4. Flujo de una petición (CQRS)
```
HTTP → Carter endpoint → (Request → Command/Query, Mapster) → ISender.Send
     → LoggingBehavior → ValidationBehavior (solo commands) → Handler
     → IApplicationDbContext / Aggregate (reglas de negocio) → SaveChangesAsync
     → AuditableEntityInterceptor → DispatchDomainEventsInterceptor → EventHandlers
     ← Result → Response (Mapster) → HTTP 200/201/204
Excepción en cualquier punto → CustomExceptionHandler → ProblemDetails (400/401/403/404/409/500)
```
- **Commands** cambian estado, devuelven un `record <Feature>Result` (p. ej. el ID creado) y se validan con FluentValidation.
- **Queries** no cambian estado, usan `AsNoTracking()` y proyectan directamente a DTOs.

## 5. Componentes de BuildingBlocks
| Componente | Ubicación | Uso |
|---|---|---|
| `ICommand`, `ICommand<T>`, `ICommandHandler<…>` | `CQRS/` | Contratos de escritura |
| `IQuery<T>`, `IQueryHandler<…>` | `CQRS/` | Contratos de lectura |
| `LoggingBehavior` | `Behaviors/` | Log de inicio/fin y aviso si una petición tarda más de 3 s |
| `ValidationBehavior` | `Behaviors/` | Ejecuta los `IValidator<TCommand>` y lanza `ValidationException` |
| Excepciones (`NotFound`, `Conflict`, `BadRequest`, `Unauthorized`, `InternalServer`) | `Exceptions/` | Errores de aplicación |
| `CustomExceptionHandler` | `Exceptions/Handler/` | Excepción → ProblemDetails con `traceId` |
| `PaginationRequest`, `PaginatedResult<T>` | `Pagination/` | Listados paginados |

**Ajustes en BuildingBlocks hechos en la feature 01 del backlog:**
- `net8.0` → `net10.0`; `JwtBearer` 8.0.30 → 10.x; versiones en `Directory.Packages.props`.
- Añadir `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, que es lo que usa `CustomExceptionHandler`.
- Quitar `StackExchange.Redis` y `Microsoft.FeatureManagement.AspNetCore` mientras no se usen.

**Ajustes pendientes en BuildingBlocks** (feature 02 del backlog):
- Crear `src/BuildingBlocks/BuildingBlocks.Domain` (abstracciones DDD + `DomainException`) y referenciarlo desde `BuildingBlocks`.
- En `CustomExceptionHandler`: mapear `DomainException` → 400 y `UnauthorizedException` → 401 (hoy cae en 500), y añadir `ForbiddenException` → 403.
- `LoggingBehavior`: `timeTaken.Seconds` → `timeTaken.TotalSeconds`.

> **Por qué `BuildingBlocks.Domain` es un proyecto aparte:** el dominio no puede depender de `BuildingBlocks`, que arrastra ASP.NET, FluentValidation, etc. Con una librería pura, `Dispensario.Domain` sigue limpio y `CustomExceptionHandler` reconoce `DomainException` por su tipo. Cualquier microservicio futuro reutilizará las mismas abstracciones.

## 6. Seguridad
- JWT HS256 emitido en `POST /api/auth/login`, con los claims `sub` (UserId), `email`, `name`, `role` y `doctor_id` (si aplica).
- Políticas: `AdminOnly` (Admin), `Reception` (Admin, Receptionist), `Clinical` (Doctor). Las lecturas generales solo exigen `RequireAuthorization()`.
- Un doctor solo puede crear y cerrar consultas propias (se valida en el handler con `ICurrentUser.DoctorId`).
- Los secretos (`Jwt:Key`, `MediatR:LicenseKey`, `Seed:Admin:Password`) van en user-secrets o variables de entorno.

## 7. Decisiones registradas (ADR resumido)
| # | Decisión | Motivo |
|---|---|---|
| 1 | Monolito modular con carpetas por módulo | Simplicidad hoy y extracción futura a microservicios |
| 2 | Un solo `DispensarioDbContext` | SQLite en un archivo, transacciones simples y una sola cadena de migraciones |
| 3 | DDD táctico (IDs tipados, VOs, domain events) | Invariantes protegidos y límites claros entre módulos |
| 4 | MediatR 14 con licencia | Ya lo usa BuildingBlocks; se configura `LicenseKey` |
| 5 | Minimal APIs + Carter | Un endpoint por feature, encaja con vertical slices |
| 6 | JWT propio, sin ASP.NET Identity | Sin acoplar tablas de Identity; más fácil de mover a un servicio de identidad |
| 7 | `IApplicationDbContext` en lugar de repositorios | EF ya es unit of work y repositorio |
| 8 | Scalar para la documentación | UI moderna sobre el OpenAPI nativo de .NET 10 |
