# SPEC 06 — Users: dominio y persistencia

> **Status:** Approved
> **Depends on:** SPEC 04, SPEC 05
> **Date:** 2026-10-26
> **Objective:** Crear el aggregate `User` (correo, teléfono opcional, identificación con tipo y número, hash de contraseña, rol, doctor vinculado opcional y estado activo) con su configuración EF Core sobre la tabla `Users` e índices únicos de email e identificación.

## Por qué este spec existe

Es el primer aggregate real del dispensario y el primero que usa el `DispensarioDbContext` con un `DbSet`.
Respecto a `DOMAIN.md` §4 añade dos datos que la guía no tenía: la identificación (`Document`) y el teléfono.
También cambia una decisión de la guía: el hash de contraseñas pasa de `PasswordHasher<T>` de Identity a **bcrypt**.
Aquí solo se documenta ese cambio; el hasher se implementa en la fila 07, junto al login.
El `Doctor` no existe hasta la fila 09, así que `DoctorId` se crea ya como ID tipado pero el vínculo es opcional.

## Scope

**In:**

- VO `Document` y enum `DocumentType` (`NationalId`, `Passport`) en `Dispensario.Domain/Common/`, compartidos con Patients (fila 11).
- ID tipado `DoctorId` en `Dispensario.Domain/Doctors/` (solo el ID; el aggregate llega en la fila 09).
- Aggregate `User`, `UserId`, enum `Role` y `UserCreatedDomainEvent` en `Dispensario.Domain/Users/`.
- Métodos de `User`: `Create`, `ChangePassword`, `UpdateProfile`, `Deactivate`, `Activate` y `LinkDoctor`.
- `DbSet<User> Users` en `IApplicationDbContext` y en `DispensarioDbContext`.
- `UserConfiguration` en `Dispensario.Infrastructure/Data/Configurations/Users/` con tabla `Users`, índice único de `Email` e índice único de (`Identification_Type`, `Identification_Number`).
- Tests unitarios de `Document` y `User`, y tests de persistencia de `User` sobre el `DispensarioDbContext` real con SQLite `:memory:`.
- Documentación: bcrypt en lugar de `PasswordHasher<T>` y el modelo de Users actualizado.

**Out of scope (for future specs):**

- `IPasswordHasher`, su implementación con bcrypt y el paquete `BCrypt.Net-Next`: fila 07.
- Login, JWT, `ICurrentUser` completo, políticas y seed del Admin: fila 07 (seed en la 17).
- Commands, queries y endpoints de usuarios (RegisterUser, ChangePassword, DeactivateUser, GetUsers): fila 08.
- Aggregate `Doctor` y la obligatoriedad de `DoctorId` cuando `Role == Doctor`: fila 09.
- Validadores de FluentValidation (no hay commands todavía).
- Migraciones: fila 17. Los tests usan `EnsureCreated`.
- Tipo de documento `ForeignerId`.

## Data model

### `Dispensario.Domain/Common/` (namespace `Dispensario.Domain.Common`)

```csharp
public enum DocumentType { NationalId, Passport }

// Documento de identidad: tipo + número
public sealed record Document : ValueObject
{
    public const int NumberMaxLength = 20;

    public DocumentType Type { get; }
    public string Number { get; }

    private Document(DocumentType type, string number) { ... }

    public static Document Create(DocumentType type, string number);
}
```

Reglas de `Document.Create` (mismo estilo que los VOs de SPEC 03):

| Regla | Mensaje (`DomainException`) |
| --- | --- |
| `type` es un valor definido del enum (`Enum.IsDefined`) | `Document type '{type}' is not valid.` |
| `number` con `Trim`; `null` cuenta como vacío | — |
| `number` no vacío | `Document number is required.` |
| `number` de como máximo `NumberMaxLength` | `Document number must not exceed 20 characters.` |

- El número se guarda tal como se escribió (solo `Trim`): sin regex ni cambio a mayúsculas.

### `Dispensario.Domain/Doctors/` (namespace `Dispensario.Domain.Doctors`)

```csharp
public readonly record struct DoctorId(Guid Value) : IStronglyTypedId
{
    public static DoctorId New() => new(Guid.CreateVersion7());
}
```

### `Dispensario.Domain/Users/` (namespace `Dispensario.Domain.Users`)

```
Users/
  User.cs  UserId.cs  Role.cs
  Events/UserCreatedDomainEvent.cs
```

```csharp
public readonly record struct UserId(Guid Value) : IStronglyTypedId
{
    public static UserId New() => new(Guid.CreateVersion7());
}

public enum Role { Admin, Doctor, Receptionist }

public sealed record UserCreatedDomainEvent(UserId UserId, Email Email, Role Role) : IDomainEvent;

public sealed class User : AggregateRoot<UserId>
{
    public Email Email { get; private set; }
    public string PasswordHash { get; private set; }
    public FullName FullName { get; private set; }
    public Document Identification { get; private set; }
    public PhoneNumber? Phone { get; private set; }
    public Role Role { get; private set; }
    public DoctorId? DoctorId { get; private set; }
    public bool IsActive { get; private set; }

    private User() { }   // EF
}
```

Métodos:

| Método | Efecto | Reglas (`DomainException`) |
| --- | --- | --- |
| `static Create(Email email, string passwordHash, FullName fullName, Document identification, PhoneNumber? phone, Role role)` | `Id = UserId.New()`, `IsActive = true`, `DoctorId = null`; emite `UserCreatedDomainEvent` | `passwordHash` no vacío → `Password hash is required.`; `role` definido → `Role '{role}' is not valid.` |
| `ChangePassword(string newPasswordHash)` | Reemplaza `PasswordHash` | no vacío → `Password hash is required.` |
| `UpdateProfile(FullName fullName, PhoneNumber? phone)` | Reemplaza nombre y teléfono (`null` borra el teléfono) | — |
| `Deactivate()` | `IsActive = false` | si ya está inactivo → `User is already inactive.` |
| `Activate()` | `IsActive = true` | si ya está activo → `User is already active.` |
| `LinkDoctor(DoctorId doctorId)` | Asigna `DoctorId` | `Role != Doctor` → `Only users with the Doctor role can be linked to a doctor.`; ya vinculado → `User is already linked to a doctor.` |

- `Email`, `FullName`, `Document` y `PhoneNumber` llegan ya validados por sus VOs: el aggregate no repite esas reglas.
- `Email` e `Identification` no se pueden cambiar en este spec.
- El dominio no hashea: recibe el hash ya calculado (lo calculará `IPasswordHasher` con bcrypt en la fila 07).

### `Dispensario.Application/Abstractions/IApplicationDbContext.cs`

```csharp
DbSet<User> Users { get; }
```

`DispensarioDbContext` añade `public DbSet<User> Users => Set<User>();`.

### `Dispensario.Infrastructure/Data/Configurations/Users/UserConfiguration.cs`

`IEntityTypeConfiguration<User>`, tabla `Users`, clave `Id` (Guid por la convención de IDs tipados).

| Propiedad | Mapeo | Columna(s) | Restricciones |
| --- | --- | --- | --- |
| `Email` | `HasConversion(e => e.Value, v => Email.Create(v))` | `Email` | requerido, `HasMaxLength(Email.MaxLength)`, **índice único** |
| `PasswordHash` | escalar | `PasswordHash` | requerido, `HasMaxLength(100)` |
| `FullName` | `ComplexProperty`, propiedades declaradas una a una | `FullName_FirstName`, `FullName_LastName` | requeridas, `HasMaxLength(FullName.MaxLength)` |
| `Identification` | `OwnsOne` en la misma tabla | `Identification_Type`, `Identification_Number` | `Type` como string (`HasMaxLength(20)`), `Number` con `HasMaxLength(Document.NumberMaxLength)`; **índice único compuesto** (`Identification_Type`, `Identification_Number`) |
| `Phone` | `HasConversion` nullable (`p => p!.Value`, `v => PhoneNumber.Create(v)`) | `Phone` | opcional, `HasMaxLength(PhoneNumber.MaxLength)` |
| `Role` | `HasConversion<string>()` | `Role` | requerido, `HasMaxLength(20)` |
| `DoctorId` | convención de IDs tipados (nullable) | `DoctorId` | opcional, sin FK (el aggregate `Doctor` no existe) |
| `IsActive` | escalar | `IsActive` | requerido |
| Auditoría | `IAuditable` de `AggregateRoot` | `CreatedAtUtc`, `CreatedBy`, `LastModifiedAtUtc`, `LastModifiedBy` | las completa `AuditableEntityInterceptor` |

- `DomainEvents` se ignora (`builder.Ignore(u => u.DomainEvents)`) si EF intenta mapearla.
- Los nombres exactos de las columnas de `ComplexProperty` y `OwnsOne` se fijan con `HasColumnName` para no depender de los valores por defecto de EF.

### Tests

| Archivo | Qué prueba |
| --- | --- |
| `tests/Dispensario.Domain.UnitTests/Common/DocumentTests.cs` | Creación válida con `Trim`; número vacío, `null` o de más de 20 caracteres → `DomainException`; tipo no definido → `DomainException`; igualdad estructural |
| `tests/Dispensario.Domain.UnitTests/Users/UserTests.cs` | `Create` deja `IsActive = true`, `DoctorId = null` y un `UserCreatedDomainEvent`; hash vacío y rol no definido → `DomainException`; `ChangePassword`, `UpdateProfile` (incluido borrar el teléfono); `Deactivate`/`Activate` y sus dobles llamadas → `DomainException`; `LinkDoctor` con rol Doctor, con otro rol y estando ya vinculado |
| `tests/Dispensario.Application.UnitTests/Persistence/Fakes/DispensarioTestDb.cs` | Igual que `TestDb` pero sobre el `DispensarioDbContext` real: SQLite `:memory:`, interceptores reales, `FakeTimeProvider`, `ICurrentUser` e `IPublisher` de NSubstitute, `EnsureCreated` y `CreateContext()` |
| `tests/Dispensario.Application.UnitTests/Persistence/Users/UserPersistenceTests.cs` | Guardar y releer un `User` con todos los campos (VOs iguales, `Role`, `DoctorId`, auditoría); teléfono y `DoctorId` `null` se releen `null`; email repetido → `DbUpdateException`; mismo tipo y número de documento → `DbUpdateException`; mismo número con distinto tipo se guarda; `Role` e `Identification_Type` se guardan como texto (consulta SQL cruda); `SaveChangesAsync` publica `UserCreatedDomainEvent` |

- Convenciones de `TESTING.md`: `Method_Scenario_ExpectedResult`, Arrange / Act / Assert y Shouldly.

## Implementation plan

1. Crear `DocumentType` y `Document` en `Domain/Common/` con `DocumentTests`. Verificar: `dotnet test` en verde.
2. Crear `DoctorId` en `Domain/Doctors/` y `UserId`, `Role` y `UserCreatedDomainEvent` en `Domain/Users/`. Verificar: compila.
3. Crear `User` con `Create`, `ChangePassword`, `UpdateProfile`, `Deactivate`, `Activate` y `LinkDoctor`, y `UserTests`. Verificar: `dotnet test` en verde.
4. Consultar la documentación de EF Core 10 (Context7) sobre `OwnsOne` con índices sobre columnas del tipo propio y sobre `HasConversion` de VOs con constructor privado. Crear `UserConfiguration`, añadir `DbSet<User> Users` a `IApplicationDbContext` y a `DispensarioDbContext`. Verificar: `dotnet build` y los tests existentes en verde (`/health` sigue respondiendo).
5. Crear `DispensarioTestDb` y `UserPersistenceTests`. Verificar: `dotnet test` en verde.
6. Actualizar la documentación:
   - `docs/guides/DOMAIN.md`: §3 añade `Document` a los VOs comunes; §4 Users con `Identification`, `Phone?`, `UpdateProfile`, reglas de `Deactivate`/`Activate`/`LinkDoctor` y `DoctorId` opcional hasta la fila 09; Patients referencia `Document` de `Common` con `DocumentType` = `NationalId`, `Passport`; §5 estructura de carpetas.
   - `docs/guides/INFRASTRUCTURE.md`: §1 `PasswordHasher.cs` pasa a `BcryptPasswordHasher.cs`; §3 VOs de un solo valor con `HasConversion` y VOs con índice con `OwnsOne`; índice único de `Users` también en (`Identification_Type`, `Identification_Number`); §6 hasher con `BCrypt.Net-Next` en lugar de `PasswordHasher<User>`; §8 registro del hasher.
   - `CLAUDE.md`: stack con hash bcrypt; "Estado actual" con el aggregate `User`; "Detalles no obvios" con el patrón de mapeo de VOs.
   - `docs/PLAN.md`: la fila 06 apunta a este spec; la pregunta abierta de tipos de documento queda cerrada (`NationalId`, `Passport`).

## Acceptance criteria

- [ ] `dotnet build Dispensario.slnx` termina sin errores y sin warnings nuevos respecto a SPEC 05.
- [ ] `dotnet test Dispensario.slnx` termina con éxito e incluye los tests de este spec y los anteriores.
- [ ] `Document.Create` recorta el número y lanza `DomainException` con número vacío, `null`, de más de 20 caracteres o con tipo no definido (cubierto por `DocumentTests`).
- [ ] `DocumentType` tiene exactamente `NationalId` y `Passport`.
- [ ] `User.Create` devuelve un usuario activo, sin `DoctorId`, con un `UserCreatedDomainEvent` con su `UserId`, `Email` y `Role`.
- [ ] `User` solo cambia de estado mediante sus métodos: todas sus propiedades tienen setter privado.
- [ ] `Deactivate` sobre un usuario inactivo, `Activate` sobre uno activo, `LinkDoctor` con rol distinto de `Doctor` y `LinkDoctor` con un doctor ya vinculado lanzan `DomainException` (cubierto por `UserTests`).
- [ ] `Dispensario.Domain` no referencia EF Core ni ningún paquete nuevo.
- [ ] `IApplicationDbContext` y `DispensarioDbContext` exponen `DbSet<User> Users`.
- [ ] La tabla `Users` tiene las columnas `Email`, `PasswordHash`, `FullName_FirstName`, `FullName_LastName`, `Identification_Type`, `Identification_Number`, `Phone`, `Role`, `DoctorId`, `IsActive` y las cuatro de auditoría.
- [ ] Guardar dos usuarios con el mismo email lanza `DbUpdateException` (cubierto por `UserPersistenceTests`).
- [ ] Guardar dos usuarios con el mismo tipo y número de documento lanza `DbUpdateException`, y con el mismo número y distinto tipo se guarda (cubierto por `UserPersistenceTests`).
- [ ] Un usuario guardado se relee con VOs iguales a los originales, y `Phone` y `DoctorId` `null` se releen `null`.
- [ ] `Role` e `Identification_Type` se guardan como texto en SQLite.
- [ ] `CreatedAtUtc` se completa al guardar un usuario nuevo.
- [ ] No existen en `src/` `IPasswordHasher`, referencias a `BCrypt` ni endpoints de usuarios.
- [ ] `DOMAIN.md`, `INFRASTRUCTURE.md` y `CLAUDE.md` describen bcrypt y el modelo de Users de este spec, y `docs/PLAN.md` fila 06 apunta a `specs/06-users-domain-and-persistence.md`.

## Decisions

- **Yes:** bcrypt para el hash de contraseñas, en lugar de `PasswordHasher<T>` de Identity. Decisión del usuario.
- **Yes:** en este spec solo se documenta bcrypt; `IPasswordHasher` y su implementación llegan en la fila 07 con el login. El dominio solo necesita un `string` con el hash.
- **No:** implementar el hasher aquí. Adelantaría la fila 07 sin un consumidor.
- **Yes:** la identificación es el VO `Document` (tipo + número), obligatorio y único por (tipo, número). Evita dos cuentas para la misma persona y se comparte con Patients.
- **Yes:** `Document` vive en `Domain/Common`, no en `Domain/Patients`. Lo usan dos módulos.
- **Yes:** `DocumentType` solo con `NationalId` y `Passport`. Decisión del usuario; `ForeignerId` se añade cuando haga falta.
- **Yes:** el número de documento solo se recorta y se limita a 20 caracteres, sin regex ni mayúsculas. Decisión del usuario.
- **No:** normalizar el número a mayúsculas con regex `^[A-Z0-9-]{4,20}$`. Descartado por el usuario (ver Risks).
- **Yes:** teléfono opcional (`PhoneNumber?`).
- **Yes:** `DoctorId` se crea ya como ID tipado, opcional al crear, y se asigna con `LinkDoctor`. La fila 08 (RegisterUser) llega antes que los doctores (fila 09).
- **No:** exigir `DoctorId` cuando `Role == Doctor` desde ya. La fila 08 no podría crear usuarios doctores. Se revisa en la fila 09.
- **No:** FK física de `Users.DoctorId` a `Doctors`. La tabla no existe; se decide en la fila 09.
- **Yes:** `Deactivate`, `Activate` y `LinkDoctor` lanzan `DomainException` si la operación no tiene efecto o ya hay vínculo. El cliente sabe que no pasó nada.
- **Yes:** `UpdateProfile(fullName, phone)` además de los métodos de la guía, para poder editar el teléfono opcional.
- **Yes:** `UserCreatedDomainEvent(UserId, Email, Role)`, como dice la guía, aunque todavía no tenga consumidor.
- **Yes:** `Email` y `Phone` se mapean con `HasConversion` a una columna. Son VOs de un solo valor, y así admiten índice único y comparaciones en LINQ (`u.Email == email`).
- **Yes:** `Identification` se mapea con `OwnsOne` en la misma tabla. El índice único compuesto necesita propiedades indexables, y `ComplexProperty` no admite índices en EF Core 10 (se confirma en el paso 4).
- **Yes:** `FullName` con `ComplexProperty`, como en SPEC 04: no necesita índice.
- **Yes:** tests de persistencia en `Application.UnitTests/Persistence/Users` con el `DispensarioDbContext` real y `EnsureCreated`. No hay migraciones hasta la fila 17.
- **No:** probar la persistencia a través de `DispensarioApiFactory`. Todavía no hay endpoints de usuarios.

## Risks

| Risk | Mitigation |
| --- | --- |
| EF Core 10 no admite índices sobre propiedades de `ComplexProperty`, o `OwnsOne` con un `record` sin clave da problemas de materialización | El paso 4 consulta la documentación de EF Core 10 antes de escribir la configuración (Context7 no estaba disponible al redactar el spec). `UserPersistenceTests` verifica la relectura y los dos índices únicos. Si `OwnsOne` falla, se para y se pregunta al usuario. |
| El número de documento no se normaliza: `ab123` y `AB123` (o `12.345` y `12345`) cuentan como distintos y el índice único no lo detecta | Aceptado por el usuario. Si se vuelve un problema, un spec posterior añade normalización y migra los datos. |
| `HasConversion` con `Email.Create` lanza `DomainException` al leer un valor inválido guardado a mano en la base de datos | Solo el dominio escribe en `Users`; la fila 17 crea la tabla con migraciones y el seed pasa por `User.Create`. |
| `DoctorId` opcional permite usuarios con rol `Doctor` sin doctor vinculado | Documentado en `DOMAIN.md`; la fila 09 decide si pasa a ser obligatorio y cómo. |
| Cambiar a bcrypt deja `INFRASTRUCTURE.md` §6 y §8 con nombres viejos si no se actualizan | El paso 6 los reescribe y un criterio de aceptación lo comprueba. |

## What is **not** in this spec

- `IPasswordHasher`, bcrypt implementado y el paquete `BCrypt.Net-Next`.
- Login, JWT, `ICurrentUser` completo, políticas y seed.
- Commands, queries, validadores y endpoints de usuarios.
- Aggregate `Doctor` y FK de `DoctorId`.
- Migraciones.
- `ForeignerId` y normalización del número de documento.

Cada uno de ellos va en su propio spec (filas 07, 08, 09, 17 o futuras).
