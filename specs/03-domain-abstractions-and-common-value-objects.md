# SPEC 03 — Abstracciones de dominio y value objects comunes

> **Status:** Approved
> **Depends on:** SPEC 02
> **Date:** 2026-10-26
> **Objective:** Añadir a `BuildingBlocks.Domain` las abstracciones DDD (`Entity<TId>`, `AggregateRoot<TId>`, `ValueObject`, `IDomainEvent`, `IAuditable`) y a `Dispensario.Domain/Common` los VOs `FullName`, `Email`, `PhoneNumber` y `Address`, con sus tests, tras mover todos los proyectos de tests a `tests/` en la raíz.

## Por qué este spec existe

`DOMAIN.md` §1 propone un `IDomainEvent` con miembros por defecto (`EventId => Guid.NewGuid()`, `OccurredOnUtc => DateTime.UtcNow`).
Esos miembros devuelven un valor distinto en cada lectura y rompen la regla de no usar `DateTime.UtcNow`.
Además, las propiedades de auditoría `CreatedAt` / `LastModifiedAt` no siguen la convención del sufijo `Utc` de SQLite.
Este spec corrige ambas cosas en el código y en las guías.
También reubica los proyectos de tests en una carpeta `tests/` plana en la raíz, por decisión del usuario, antes de añadir tests nuevos.

## Scope

**In:**

- Mover los cuatro proyectos de tests a `tests/` en la raíz (estructura plana), con `git mv`, corrigiendo sus `ProjectReference` y `Dispensario.slnx`.
- `IDomainEvent` como interfaz marcadora que hereda de `INotification`.
- `IAuditable` con `CreatedAtUtc`, `CreatedBy`, `LastModifiedAtUtc` y `LastModifiedBy`.
- `Entity<TId>` con igualdad por tipo concreto + `Id`, `IEquatable<Entity<TId>>`, `==` y `!=`.
- `AggregateRoot<TId>` con la lista de domain events (`Raise`, `DomainEvents`, `ClearDomainEvents`) que implementa `IAuditable`.
- `ValueObject` como `abstract record`.
- VOs `FullName`, `Email`, `PhoneNumber` y `Address` en `Dispensario.Domain/Common`, cada uno `sealed record` con constructor privado y `static Create(...)` que valida y lanza `DomainException`.
- Tests de las abstracciones en `tests/BuildingBlocks.UnitTests/Domain/`.
- Tests de los VOs en `tests/Dispensario.Domain.UnitTests/Common/`.
- Actualizar `CLAUDE.md`, `docs/PLAN.md`, `docs/ARCHITECTURE.md`, `docs/guides/DOMAIN.md`, `docs/guides/INFRASTRUCTURE.md` y `docs/guides/TESTING.md`.

**Out of scope (for future specs):**

- IDs tipados concretos (`PatientId`, `DoctorId`…): llegan con cada aggregate.
- Metadatos de los eventos (`EventId`, `OccurredOnUtc`): se añaden si llega el *outbox*.
- `AuditableEntityInterceptor`, `DispatchDomainEventsInterceptor` y el mapeo EF de los VOs (`ComplexProperty`): fila 04.
- VO `Document` y enums de Patients (`DocumentType`, `Gender`, `BloodType`): fila 11.
- Validadores FluentValidation que repliquen las reglas de los VOs: en el spec de cada command.
- Normalización del teléfono a solo dígitos.
- Modificar los specs 01 y 02 (son históricos y conservan las rutas antiguas).

## Data model

### Estructura de tests (tras el movimiento)

| Proyecto | Ruta anterior | Ruta nueva |
| --- | --- | --- |
| `BuildingBlocks.UnitTests` | `src/BuildingBlocks/tests/BuildingBlocks.UnitTests` | `tests/BuildingBlocks.UnitTests` |
| `Dispensario.Domain.UnitTests` | `src/Services/Dispensario/tests/Dispensario.Domain.UnitTests` | `tests/Dispensario.Domain.UnitTests` |
| `Dispensario.Application.UnitTests` | `src/Services/Dispensario/tests/Dispensario.Application.UnitTests` | `tests/Dispensario.Application.UnitTests` |
| `Dispensario.Api.IntegrationTests` | `src/Services/Dispensario/tests/Dispensario.Api.IntegrationTests` | `tests/Dispensario.Api.IntegrationTests` |

- Las referencias no cambian, solo su ruta relativa (p. ej. `..\..\src\Services\Dispensario\Dispensario.Domain\Dispensario.Domain.csproj`).
- En `Dispensario.slnx`, las carpetas `/src/BuildingBlocks/tests/` y `/src/Services/Dispensario/tests/` se sustituyen por una sola carpeta `/tests/` con los cuatro proyectos.
- Las carpetas `src/BuildingBlocks/tests/` y `src/Services/Dispensario/tests/` dejan de existir.

### Abstracciones (`src/BuildingBlocks/BuildingBlocks.Domain/`, namespace `BuildingBlocks.Domain`)

Contenido orientativo, un tipo por archivo:

```csharp
// IDomainEvent.cs — marcador: hecho de negocio ocurrido dentro de un aggregate
public interface IDomainEvent : INotification;

// IAuditable.cs — lo completa AuditableEntityInterceptor (fila 04)
public interface IAuditable
{
    DateTime CreatedAtUtc { get; set; }
    string? CreatedBy { get; set; }
    DateTime? LastModifiedAtUtc { get; set; }
    string? LastModifiedBy { get; set; }
}

// Entity.cs
public abstract class Entity<TId> : IEquatable<Entity<TId>> where TId : notnull
{
    public TId Id { get; protected set; } = default!;
    // Equals: mismo tipo concreto (GetType()) y Id igual (EqualityComparer<TId>.Default)
    // GetHashCode: HashCode.Combine(GetType(), Id)
    // == y != delegan en Equals y admiten null en ambos lados
}

// AggregateRoot.cs
public abstract class AggregateRoot<TId> : Entity<TId>, IAuditable where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }
}

// ValueObject.cs — los records ya dan igualdad estructural
public abstract record ValueObject;
```

### Value objects comunes (`src/Services/Dispensario/Dispensario.Domain/Common/`, namespace `Dispensario.Domain.Common`)

Forma común de cada VO:

```csharp
public sealed record Email : ValueObject
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string value) { /* normaliza, valida y lanza DomainException */ }

    public override string ToString() => Value;
}
```

- Las propiedades son de solo lectura.
- El constructor privado recibe parámetros con el mismo nombre que las propiedades, para que EF Core pueda enlazarlos en la fila 04.
- `Create` recorta (`Trim`) todos los textos antes de validar.
- Una entrada `null` se trata como vacía.
- Los mensajes de `DomainException` están en inglés.

| VO | Campos | Normalización | Reglas (si no se cumplen → `DomainException`) | `ToString()` |
| --- | --- | --- | --- | --- |
| `FullName` | `FirstName`, `LastName` | Trim | ambos obligatorios, máx. 100 cada uno | `"First Last"` |
| `Email` | `Value` | Trim + `ToLowerInvariant()` | obligatorio, máx. 254, regex `^[^@\s]+@[^@\s]+\.[^@\s]+$` | `Value` |
| `PhoneNumber` | `Value` | Trim (se guarda tal como se escribió) | obligatorio, 7–20 caracteres, solo dígitos, `+`, espacios y guiones: regex `^[0-9+\- ]{7,20}$` | `Value` |
| `Address` | `Street`, `City`, `State?`, `Reference?` | Trim; `State` y `Reference` vacíos o en blanco → `null` | `Street` obligatorio, máx. 200; `City` obligatorio, máx. 100; `State` máx. 100; `Reference` máx. 250 | por defecto del record |

Firmas de las fábricas:

```csharp
FullName.Create(string firstName, string lastName)
Email.Create(string value)
PhoneNumber.Create(string value)
Address.Create(string street, string city, string? state = null, string? reference = null)
```

### Tests

| Archivo | Qué prueba |
| --- | --- |
| `tests/BuildingBlocks.UnitTests/Domain/EntityTests.cs` | Igualdad por tipo + Id, desigualdad con otro Id o con otro tipo con el mismo Id, `==`/`!=` con null, `GetHashCode` coherente |
| `tests/BuildingBlocks.UnitTests/Domain/AggregateRootTests.cs` | `Raise` añade eventos en orden, `ClearDomainEvents` los vacía, `DomainEvents` no se puede convertir en `List` modificable, implementa `IAuditable` |
| `tests/BuildingBlocks.UnitTests/Domain/ValueObjectTests.cs` | Igualdad estructural, y dos VOs de tipos distintos con los mismos valores no son iguales |
| `tests/Dispensario.Domain.UnitTests/Common/FullNameTests.cs` | Creación válida con Trim, vacíos, longitud 100 OK y 101 falla, `ToString()` |
| `tests/Dispensario.Domain.UnitTests/Common/EmailTests.cs` | Normalización a minúsculas y Trim, formatos inválidos, 254 OK y 255 falla, igualdad entre mayúsculas y minúsculas |
| `tests/Dispensario.Domain.UnitTests/Common/PhoneNumberTests.cs` | Formatos válidos (`+51 999-111-222`), letras, 6 y 21 caracteres fallan, 7 y 20 pasan, se conserva el formato |
| `tests/Dispensario.Domain.UnitTests/Common/AddressTests.cs` | Obligatorios, máximos de cada campo, opcionales en blanco → `null` |

- Las clases de prueba concretas que necesiten las abstracciones (p. ej. una entidad `TestEntity`, un `TestEvent`) se declaran como tipos `private` anidados o `file` dentro del archivo de tests.
- Nombres `Method_Scenario_ExpectedResult` y estructura Arrange / Act / Assert, según `TESTING.md`.
- Los casos con varias entradas usan `[Theory]` con `[InlineData]`.

## Implementation plan

1. Mover con `git mv` los cuatro proyectos de tests a `tests/` (estructura plana), corregir las rutas de sus `ProjectReference` y sustituir en `Dispensario.slnx` las dos carpetas de tests por `/tests/`. Borrar las carpetas vacías `src/BuildingBlocks/tests/` y `src/Services/Dispensario/tests/`. Verificar: `dotnet build Dispensario.slnx` compila y `dotnet test Dispensario.slnx` pasa los tres tests de `CustomExceptionHandlerTests`.
2. Crear `IDomainEvent.cs` e `IAuditable.cs` en `BuildingBlocks.Domain`. Verificar: compila.
3. Crear `Entity.cs` y `tests/BuildingBlocks.UnitTests/Domain/EntityTests.cs`. Verificar: `dotnet test` en verde.
4. Crear `AggregateRoot.cs` y `AggregateRootTests.cs`. Verificar: `dotnet test` en verde.
5. Crear `ValueObject.cs` y `ValueObjectTests.cs`. Verificar: `dotnet test` en verde.
6. Crear `Common/FullName.cs` en `Dispensario.Domain` y `FullNameTests.cs`. Verificar: `dotnet test` en verde.
7. Crear `Common/Email.cs` y `EmailTests.cs`. Verificar: `dotnet test` en verde.
8. Crear `Common/PhoneNumber.cs` y `PhoneNumberTests.cs`. Verificar: `dotnet test` en verde.
9. Crear `Common/Address.cs` y `AddressTests.cs`. Verificar: `dotnet test` en verde.
10. Actualizar la documentación:
    - `docs/guides/DOMAIN.md` §1: `IDomainEvent` marcador, auditoría con sufijo `Utc`, igualdad de `Entity` (tipo + Id, operadores). §3: normalización, regex y longitudes máximas de cada VO.
    - `docs/guides/INFRASTRUCTURE.md` §5: `CreatedAtUtc` / `LastModifiedAtUtc`.
    - `docs/guides/TESTING.md`: proyectos en `tests/` y fila de `BuildingBlocks.UnitTests` en la tabla.
    - `docs/ARCHITECTURE.md`: la ruta de `BuildingBlocks.UnitTests` pasa a `tests/BuildingBlocks.UnitTests`.
    - `CLAUDE.md`: "Estado actual", árbol de "Estructura" con `tests/` en la raíz y comando `dotnet test tests/Dispensario.Domain.UnitTests --filter ...`.
    - `docs/PLAN.md`: la fila 03 apunta a este spec.

## Acceptance criteria

- [ ] Existen `tests/BuildingBlocks.UnitTests`, `tests/Dispensario.Domain.UnitTests`, `tests/Dispensario.Application.UnitTests` y `tests/Dispensario.Api.IntegrationTests`.
- [ ] No existen `src/BuildingBlocks/tests/` ni `src/Services/Dispensario/tests/`.
- [ ] `dotnet sln Dispensario.slnx list` muestra los 10 proyectos, y los cuatro de tests con ruta `tests/...`.
- [ ] `dotnet build Dispensario.slnx` termina sin errores y sin warnings nuevos respecto a SPEC 02.
- [ ] `dotnet test Dispensario.slnx` termina con éxito e incluye los tests de `CustomExceptionHandlerTests` y de las siete clases de tests de este spec.
- [ ] `BuildingBlocks.Domain` contiene `IDomainEvent`, `IAuditable`, `Entity<TId>`, `AggregateRoot<TId>` y `ValueObject`, y sigue sin referencias a proyectos ni más paquetes que `MediatR.Contracts`.
- [ ] `IDomainEvent` hereda de `INotification` y no declara miembros.
- [ ] Ningún archivo de `BuildingBlocks.Domain` ni de `Dispensario.Domain` contiene `DateTime.Now`, `DateTime.UtcNow` ni `Guid.NewGuid`.
- [ ] Dos entidades del mismo tipo y mismo Id son iguales con `Equals` y con `==`; con distinto tipo o distinto Id no lo son (cubierto por `EntityTests`).
- [ ] `AggregateRoot<TId>` implementa `IAuditable` con `CreatedAtUtc`, `CreatedBy`, `LastModifiedAtUtc` y `LastModifiedBy`.
- [ ] Tras `Raise` de dos eventos, `DomainEvents` los contiene en orden; tras `ClearDomainEvents`, está vacío (cubierto por `AggregateRootTests`).
- [ ] `FullName.Create(" Ana ", " Pérez ").ToString()` devuelve `"Ana Pérez"`; un nombre vacío o de 101 caracteres lanza `DomainException`.
- [ ] `Email.Create(" Ana@Mail.COM ")` tiene `Value == "ana@mail.com"`; `"ana"`, `"ana@mail"` y uno de 255 caracteres lanzan `DomainException`.
- [ ] `PhoneNumber.Create("+51 999-111-222")` conserva ese valor; `"12345a7"`, uno de 6 caracteres y uno de 21 lanzan `DomainException`.
- [ ] `Address.Create("Av. Sol 123", "Lima", "  ", null)` tiene `State == null`; `Street` vacío, `City` vacío o un campo por encima de su máximo lanzan `DomainException`.
- [ ] Los cuatro VOs son `sealed record` que heredan de `ValueObject` y no tienen constructor público.
- [ ] `DOMAIN.md`, `INFRASTRUCTURE.md`, `TESTING.md`, `ARCHITECTURE.md` y `CLAUDE.md` ya no mencionan `CreatedAt` sin sufijo, `DateTime.UtcNow` en `IDomainEvent` ni las rutas `src/.../tests/`.
- [ ] `docs/PLAN.md` fila 03 apunta a `specs/03-domain-abstractions-and-common-value-objects.md`.

## Decisions

- **Yes:** `IDomainEvent` como marcador puro. El dominio no conoce la hora y un `Guid.NewGuid()` en un miembro por defecto cambia en cada lectura.
- **No:** propiedades `EventId` / `OccurredOnUtc` obligatorias ni un record base `DomainEvent`. Obligarían a pasar la fecha a cada evento sin que nadie la use todavía; se añaden con el *outbox*.
- **Yes:** auditoría con sufijo `Utc` (`CreatedAtUtc`, `LastModifiedAtUtc`), como exige la convención de fechas de SQLite. Se corrigen las guías.
- **No:** mantener `CreatedAt` / `LastModifiedAt` de `DOMAIN.md`. Sería una excepción a la convención en la primera tabla que la usa.
- **Yes:** setters públicos en las propiedades de auditoría. Los escribe el interceptor de Infrastructure, fuera del aggregate.
- **Yes:** `Entity<TId>` igual por tipo concreto + Id, con `IEquatable` y operadores. Evita que un `==` compare referencias por error.
- **No:** igualdad solo por Id. Dos entidades de tipos distintos con IDs del mismo tipo base se considerarían iguales.
- **No:** concepto de entidad transitoria (Id por defecto). Los IDs tipados se generan en `Create` con `Guid.CreateVersion7()`, así que una entidad nunca existe sin Id.
- **Yes:** `ValueObject` como `abstract record` y VOs `sealed record` con `Create`. Los records dan igualdad estructural y el `EqualityContract` distingue tipos.
- **No:** `ValueObject` clásico con `GetEqualityComponents()`. Más código para el mismo resultado.
- **Yes:** los VOs validan sus invariantes y lanzan `DomainException`, aunque FluentValidation repita algunas reglas. Un VO no puede existir en un estado inválido.
- **Yes:** Trim en todos los textos y teléfono guardado tal como se escribió. Es lo que el usuario ve y teclea.
- **No:** normalizar el teléfono a solo dígitos. Se puede hacer en otro spec si aparece la búsqueda por teléfono.
- **Yes:** regex simple para el email con máximo 254. Es predecible y se puede repetir igual en FluentValidation.
- **No:** `MailAddress.TryCreate`. Acepta formatos raros (nombres con comillas, display names) que no queremos.
- **Yes:** regex literal de `DOMAIN.md` para el teléfono (`^[0-9+\- ]{7,20}$`). No se exige un número mínimo de dígitos.
- **Yes:** máximos de `Address`: Street 200, City 100, State 100, Reference 250.
- **Yes:** mover todos los tests a `tests/` en la raíz con estructura plana, por decisión del usuario. Se hace como primer paso para que los tests nuevos nazcan en su sitio.
- **No:** estructura espejo de `src/` dentro de `tests/`. Con cuatro proyectos no aporta nada.
- **Yes:** `git mv` para conservar el historial de los archivos movidos.
- **Yes:** tests de las abstracciones en `tests/BuildingBlocks.UnitTests/Domain/`. El proyecto ya ve `BuildingBlocks.Domain` de forma transitiva.
- **No:** un proyecto `BuildingBlocks.Domain.UnitTests` aparte. Un proyecto más para tres clases de tests.
- **Yes:** los specs 01 y 02 no se tocan. Son el registro de lo que se hizo en su momento.

## Risks

| Risk | Mitigation |
| --- | --- |
| El constructor privado o las propiedades de solo lectura de los VOs no encajan con `ComplexProperty` de EF Core 10 | El constructor recibe parámetros con los mismos nombres que las propiedades (enlace por constructor de EF). Se comprueba en la fila 04; si falla, se ajusta allí. |
| `ToString()` sobrescrito en un `sealed record` entra en conflicto con el generado | C# permite `public override string ToString()` en records; se cubre con los tests de `FullName` y `Email`. |
| `git mv` en Windows deja carpetas `bin/` y `obj/` antiguas en `src/.../tests/` | Se borran las carpetas antiguas y se comprueba con el criterio de aceptación correspondiente. |
| Algún archivo (docs, scripts, `.gitignore`) sigue apuntando a `src/.../tests/` | Se busca `src/BuildingBlocks/tests` y `src/Services/Dispensario/tests` en el repositorio, excluidos `specs/01` y `specs/02`, antes de cerrar el paso 10. |

## What is **not** in this spec

- IDs tipados concretos y aggregates de los módulos.
- Metadatos de los domain events y *outbox*.
- Interceptores de auditoría y de eventos, y el mapeo EF de los VOs.
- VO `Document` y enums de Patients.
- Validadores FluentValidation de los VOs.
- Normalización del teléfono a solo dígitos.
- Cambios en los specs 01 y 02.

Cada uno de ellos va en su propio spec (la mayoría en las filas 04, 06–11 del backlog).
