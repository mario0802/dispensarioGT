# SPEC 11 — Patients: CRUD

> **Status:** Implemented
> **Depends on:** SPEC 07, SPEC 08, SPEC 09
> **Date:** 2026-10-27
> **Objective:** Crear el aggregate `Patient` con documento único y exponer su alta, edición, activación, desactivación, listado con búsqueda, detalle y búsqueda por documento.

## Por qué este spec existe

Agendar citas (fila 12) y abrir consultas (fila 15) necesitan pacientes existentes y activos.
La recepción busca al paciente por su documento al llegar, por eso hay una ruta propia para esa búsqueda.
`DOMAIN.md` proponía dos métodos de edición (`UpdatePersonalData` y `UpdateMedicalData`) y un `PatientRegisteredDomainEvent`: este spec los sustituye por un único `Update` y no emite eventos (decisiones del usuario).
Sigue el patrón de Doctors (spec 09): mismo estilo de aggregate, paginación, filtros y activación.

## Scope

**In:**

- Aggregate `Patient`, ID tipado `PatientId` y enums `Gender` y `BloodType` en `Dispensario.Domain/Patients/`, con sus tests de dominio.
- Persistencia: `PatientConfiguration` (tabla `Patients`, índice único del documento) y `DbSet<Patient>` en `IApplicationDbContext` y `DispensarioDbContext`.
- Commands en `Dispensario.Application/Patients/Commands/`: `CreatePatient`, `UpdatePatient`, `DeactivatePatient` y `ActivatePatient`, con validadores y handlers.
- Queries en `Dispensario.Application/Patients/Queries/`: `GetPatients` (paginada, con `search` e `isActive`), `GetPatientById` y `GetPatientByDocument`.
- `PatientDto`, `AddressDto`, `PatientsMappingConfig` y el helper `BloodTypeNotation` (`"A+"` ↔ `BloodType.APositive`).
- Endpoints `POST /api/patients`, `PUT /api/patients/{id}`, `POST /api/patients/{id}/deactivate` y `POST /api/patients/{id}/activate` (`Reception`), y `GET /api/patients`, `GET /api/patients/{id}` y `GET /api/patients/by-document` (cualquier usuario autenticado).
- Tests unitarios de dominio, persistencia, validadores y handlers, y tests de integración de los endpoints.
- Documentación: sección **Endpoints** del `README.md`, `DOMAIN.md`, `APPLICATION.md`, `INFRASTRUCTURE.md`, `API.md`, `CLAUDE.md` y `docs/PLAN.md`.

**Out of scope (for future specs):**

- Historia clínica del paciente (consultas anteriores): fila 15.
- Comprobar que el paciente está activo al agendar: lo hace el handler de la fila 12.
- Que un `Doctor` edite los datos médicos (tipo de sangre, alergias): la escritura es solo de `Reception`.
- Domain events en Patients (`PatientRegisteredDomainEvent`…): no hay consumidores.
- Normalizar el número de documento a mayúsculas (cambiaría también Users).
- Nuevos tipos de documento (`ForeignerId`).
- Filtro por género en el listado.
- Límite inferior de la fecha de nacimiento.
- Borrado de pacientes (solo se desactivan) y fusión de pacientes duplicados.
- Migraciones: siguen con `EnsureCreated` hasta la fila 17.

## Data model

### Aggregate `Patient` (`Dispensario.Domain/Patients/`)

```
Patients/
  Patient.cs     PatientId.cs
  Gender.cs      BloodType.cs
```

```csharp
public readonly record struct PatientId(Guid Value) : IStronglyTypedId
{
    public static PatientId New() => new(Guid.CreateVersion7());
}

public enum Gender { Male, Female, Other }

public enum BloodType
{
    APositive, ANegative, BPositive, BNegative,
    ABPositive, ABNegative, OPositive, ONegative
}

public sealed class Patient : AggregateRoot<PatientId>
{
    public const int AllergiesMaxLength = 1000;

    public Document Document { get; private set; }          // único (Type + Number); editable
    public FullName FullName { get; private set; }
    public DateOnly BirthDate { get; private set; }         // no futura
    public Gender Gender { get; private set; }
    public Email? Email { get; private set; }               // opcional
    public PhoneNumber? Phone { get; private set; }         // opcional
    public Address? Address { get; private set; }           // opcional
    public BloodType? BloodType { get; private set; }       // opcional
    public string? Allergies { get; private set; }          // Trim; en blanco → null
    public bool IsActive { get; private set; }              // true al crear

    public static Patient Create(Document document, FullName fullName, DateOnly birthDate, Gender gender,
        Email? email, PhoneNumber? phone, Address? address, BloodType? bloodType, string? allergies,
        DateOnly today);
    public void Update(Document document, FullName fullName, DateOnly birthDate, Gender gender,
        Email? email, PhoneNumber? phone, Address? address, BloodType? bloodType, string? allergies,
        DateOnly today);   // reemplaza todo: un opcional null borra el valor actual
    public void Deactivate();
    public void Activate();
}
```

Reglas de dominio (`DomainException` → 400):

| Regla | Mensaje |
| --- | --- |
| `BirthDate` posterior a `today` | `Birth date cannot be in the future.` |
| `Gender` no definido | `Gender is invalid.` |
| `BloodType` no null y no definido | `Blood type is invalid.` |
| `Allergies` de más de 1000 caracteres tras el `Trim` | `Allergies must be at most 1000 characters.` |
| `Deactivate` sobre un inactivo | `Patient is already inactive.` |
| `Activate` sobre un activo | `Patient is already active.` |

- `Create` y `Update` aplican las mismas reglas y la misma normalización.
- `today` lo calcula el handler con `TimeProvider` (`DateOnly.FromDateTime(GetUtcNow().UtcDateTime)`): el dominio no conoce la hora actual (`DOMAIN.md` §2 regla 8). La fecha de hoy es válida (recién nacido).
- `Update` también se permite sobre un paciente inactivo.
- El dominio no conoce la notación `"A+"`: solo el enum.
- Sin domain events.

### Persistencia (`Dispensario.Infrastructure/Data/Configurations/Patients/PatientConfiguration.cs`, nuevo)

Tabla `Patients`:

| Columna | Origen | Notas |
| --- | --- | --- |
| `Id` | `PatientId` | conversión por convención |
| `Document_Type`, `Document_Number` | `OwnsOne` | tipo como texto, número `Document.NumberMaxLength`, requeridas, **índice único compuesto**; `Navigation(...).IsRequired()` |
| `FullName_FirstName`, `FullName_LastName` | `ComplexProperty` | `FullName.MaxLength`, requeridas |
| `BirthDate` | `DateOnly` | TEXT en SQLite, requerida |
| `Gender` | `HasConversion<string>()` | requerida |
| `Email` | `HasConversion` | `Email.MaxLength`, nullable, sin índice |
| `Phone` | `HasConversion` | `PhoneNumber.MaxLength`, nullable |
| `Address_Street`, `Address_City`, `Address_State`, `Address_Reference` | `ComplexProperty` opcional | máximos de `Address`; `Street` y `City` requeridas dentro del VO (las columnas son nullable porque el VO es opcional) |
| `BloodType` | `HasConversion<string>()` | nullable; guarda el nombre del enum (`APositive`) |
| `Allergies` | `string` | `Patient.AllergiesMaxLength`, nullable |
| `IsActive` | `bool` | requerida |
| auditoría | `IAuditable` | como en `Users` y `Doctors` |

- Cambios en código existente: `IApplicationDbContext` y `DispensarioDbContext` añaden `DbSet<Patient> Patients`.
- Sin índice sobre `LastName`: está dentro de un `ComplexProperty`, que no admite índices. Se corrige la tabla de índices de `INFRASTRUCTURE.md`.
- El esquema cambia: hay que **borrar `dispensario.db`** local antes de `dotnet run`.

### `Dispensario.Application/Patients/` (namespaces iguales a las carpetas)

```
Patients/
  Commands/
    CreatePatient/      CreatePatientCommand.cs  CreatePatientHandler.cs
    UpdatePatient/      UpdatePatientCommand.cs  UpdatePatientHandler.cs
    DeactivatePatient/  DeactivatePatientCommand.cs  DeactivatePatientHandler.cs
    ActivatePatient/    ActivatePatientCommand.cs  ActivatePatientHandler.cs
  Queries/
    GetPatients/          GetPatientsQuery.cs  GetPatientsHandler.cs
    GetPatientById/       GetPatientByIdQuery.cs  GetPatientByIdHandler.cs
    GetPatientByDocument/ GetPatientByDocumentQuery.cs  GetPatientByDocumentHandler.cs
  Dtos/                 PatientDto.cs  AddressDto.cs  PatientsMappingConfig.cs
  BloodTypeNotation.cs
```

```csharp
public sealed record CreatePatientCommand(
    DocumentType DocumentType, string DocumentNumber,
    string FirstName, string LastName,
    DateOnly BirthDate, Gender Gender,
    string? Email, string? Phone,
    AddressInput? Address,
    string? BloodType,            // "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-" o null
    string? Allergies) : ICommand<CreatePatientResult>;
public sealed record AddressInput(string Street, string City, string? State, string? Reference);
public sealed record CreatePatientResult(Guid Id);

public sealed record UpdatePatientCommand(
    Guid PatientId,
    /* los mismos campos que CreatePatientCommand */) : ICommand;

public sealed record DeactivatePatientCommand(Guid PatientId) : ICommand;
public sealed record ActivatePatientCommand(Guid PatientId) : ICommand;

public sealed record GetPatientsQuery(PaginationRequest Pagination, string? Search, bool? IsActive)
    : IQuery<PaginatedResult<PatientDto>>;
public sealed record GetPatientByIdQuery(Guid PatientId) : IQuery<PatientDto>;
public sealed record GetPatientByDocumentQuery(DocumentType DocumentType, string DocumentNumber) : IQuery<PatientDto>;

public sealed record PatientDto(
    Guid Id, DocumentType DocumentType, string DocumentNumber,
    string FirstName, string LastName,
    DateOnly BirthDate, Gender Gender,
    string? Email, string? Phone, AddressDto? Address,
    string? BloodType,            // notación clínica: "A+"
    string? Allergies, bool IsActive, DateTime CreatedAtUtc);
public sealed record AddressDto(string Street, string City, string? State, string? Reference);
```

- `BloodTypeNotation` (estática): `bool TryParse(string? value, out BloodType bloodType)` y `string ToNotation(BloodType bloodType)`. Acepta exactamente los ocho valores clínicos, en mayúsculas y sin espacios.
- `PatientsMappingConfig` (`IRegister`, Mapster): `Patient` → `PatientDto` con los IDs a `Guid`, los VOs a sus valores, `Address` a `AddressDto` o `null`, y `BloodType` con `BloodTypeNotation.ToNotation` o `null`.

### Validadores (en el archivo de cada command)

| Command | Reglas |
| --- | --- |
| `CreatePatientCommand` | `DocumentType`: `IsInEnum`. `DocumentNumber`: `NotEmpty`, `MaximumLength(Document.NumberMaxLength)`. `FirstName`/`LastName`: `NotEmpty`, `MaximumLength(FullName.MaxLength)`. `BirthDate`: `NotEmpty`. `Gender`: `IsInEnum`. `Email`: si no es null, `MaximumLength(Email.MaxLength)` y `EmailAddress`. `Phone`: `MaximumLength(PhoneNumber.MaxLength)` si no es null. `Address`: si no es null, `Street` `NotEmpty` y `MaximumLength(Address.StreetMaxLength)`, `City` `NotEmpty` y `MaximumLength(Address.CityMaxLength)`, `State` y `Reference` con sus máximos. `BloodType`: si no es null, `BloodTypeNotation.TryParse` con el mensaje `Blood type must be one of A+, A-, B+, B-, AB+, AB-, O+, O-.`. `Allergies`: `MaximumLength(Patient.AllergiesMaxLength)` si no es null |
| `UpdatePatientCommand` | `PatientId`: `NotEmpty`. El resto como en `CreatePatientCommand` |
| `DeactivatePatientCommand` / `ActivatePatientCommand` | `PatientId`: `NotEmpty` |

- Las longitudes se validan con los valores sin recortar; los VOs y el dominio las vuelven a comprobar tras el `Trim`.
- La fecha no futura es regla de dominio (necesita `today`), no del validador.
- Un `email` o `phone` en blanco no es null: lo rechaza el validador o el VO (400).

### Handlers

Los commands reciben `IApplicationDbContext`; `CreatePatientHandler` y `UpdatePatientHandler` también `TimeProvider`. Cada command hace un único `SaveChangesAsync`.

| Handler | Comportamiento, en orden |
| --- | --- |
| `CreatePatientHandler` | Crea los VOs (`Address.Create` solo si `Address` no es null), traduce `BloodType` con `BloodTypeNotation` y crea el `Patient` con la fecha de hoy. Si existe otro paciente con el mismo `Document.Type` y `Document.Number` (ya recortado) → `ConflictException("Patient with document {document} already exists.")`. Guarda y devuelve el ID |
| `UpdatePatientHandler` | No existe → `NotFoundException(nameof(Patient), id)`. `Update(...)` con la fecha de hoy. Si otro paciente (ID distinto) tiene el mismo documento → `ConflictException` con el mismo mensaje. Guarda |
| `DeactivatePatientHandler` | No existe → `NotFoundException`. `Deactivate()` (inactivo → 400 de dominio) y guarda |
| `ActivatePatientHandler` | No existe → `NotFoundException`. `Activate()` (activo → 400 de dominio) y guarda |
| `GetPatientsHandler` | `PageIndex < 0` o `PageSize` fuera de 1–100 → `BadRequestException`. `AsNoTracking`; filtros opcionales: `IsActive`; `Search` (recortado; contiene, con `EF.Functions.Like` y `LikePatterns.Contains`, en nombre, apellido o número de documento). Orden por apellido, nombre e ID. `ProjectToType<PatientDto>()` y devuelve `PaginatedResult<PatientDto>` |
| `GetPatientByIdHandler` | `AsNoTracking`; no existe → `NotFoundException`; si no, `PatientDto` |
| `GetPatientByDocumentHandler` | Número en blanco → `BadRequestException("Document number is required.")`. `AsNoTracking`; busca por `Type` y número recortado (igualdad exacta, como el índice único). No existe → `NotFoundException("Patient with document {document} was not found.")`; si no, `PatientDto`. Devuelve también pacientes inactivos |

- La unicidad se comprueba comparando `p.Document.Type` y `p.Document.Number` por separado, no el VO entero (`OwnsOne` no se compara como valor en LINQ).
- El número de documento distingue mayúsculas (`ab123` ≠ `AB123`), igual que en Users.
- Las queries no pasan por `ValidationBehavior`: `GetPatientsHandler` y `GetPatientByDocumentHandler` validan a mano.

### `Dispensario.Api/Endpoints/Patients/`

```
Patients/
  CreatePatientEndpoint.cs  UpdatePatientEndpoint.cs
  DeactivatePatientEndpoint.cs  ActivatePatientEndpoint.cs
  GetPatientsEndpoint.cs  GetPatientByIdEndpoint.cs  GetPatientByDocumentEndpoint.cs
```

| Ruta | Request → Response | Autorización | Respuestas |
| --- | --- | --- | --- |
| `POST /api/patients` | `CreatePatientRequest` (los campos del command, con `AddressRequest?`) → `CreatePatientResponse(Guid Id)` | `Reception` | 201 (`Location: /api/patients/{id}`), 400, 401, 403, 409 |
| `GET /api/patients` | `[AsParameters] GetPatientsRequest(int PageIndex = 0, int PageSize = 10, string? Search, bool? IsActive)` → `PaginatedResult<PatientResponse>` | `RequireAuthorization()` | 200, 400, 401 |
| `GET /api/patients/{id:guid}` | — → `PatientResponse` (campos de `PatientDto`, con `AddressResponse?`) | `RequireAuthorization()` | 200, 401, 404 |
| `GET /api/patients/by-document` | `[AsParameters] GetPatientByDocumentRequest(DocumentType Type, string Number)` → `PatientResponse` | `RequireAuthorization()` | 200, 400, 401, 404 |
| `PUT /api/patients/{id:guid}` | `UpdatePatientRequest` (los mismos campos que el alta) | `Reception` | 204, 400, 401, 403, 404, 409 |
| `POST /api/patients/{id:guid}/deactivate` | — | `Reception` | 204, 400, 401, 403, 404 |
| `POST /api/patients/{id:guid}/activate` | — | `Reception` | 204, 400, 401, 403, 404 |

Ejemplo de cuerpo del alta (`documentType` y `gender` viajan como texto por el `JsonStringEnumConverter` global; `birthDate` como `"yyyy-MM-dd"`):

```json
{
  "documentType": "NationalId",
  "documentNumber": "0102030405",
  "firstName": "Ana",
  "lastName": "Pérez",
  "birthDate": "1990-05-14",
  "gender": "Female",
  "email": "ana@example.com",
  "phone": "+593 99 123 4567",
  "address": { "street": "Av. Siempre Viva 742", "city": "Quito", "state": null, "reference": null },
  "bloodType": "O+",
  "allergies": "Penicillin"
}
```

- `GET /api/patients/by-document` se declara como ruta literal: no choca con `/{id:guid}`.
- Un `documentType` o `gender` desconocido, o un `birthDate` mal formado, falla al leer la petición: 400 de `BadHttpRequestException`, sin `ValidationErrors` (spec 10). Un `bloodType` desconocido sí da 400 con `ValidationErrors`.
- Todos con `WithTags("Patients")`, `WithName` igual al command o query (`CreatePatient`, `GetPatientByDocument`…) y `WithSummary`.

### Tests

| Archivo | Qué prueba |
| --- | --- |
| `tests/Dispensario.Domain.UnitTests/Patients/PatientTests.cs` | `Create` deja el paciente activo con todos los campos; alergias recortadas y en blanco → null; nacimiento hoy pasa y mañana → `DomainException`; género o tipo de sangre fuera del enum → `DomainException`; alergias de 1001 caracteres → `DomainException`. `Update` cambia todos los campos (incluido el documento) y los opcionales null los borran; vale sobre un inactivo. `Deactivate` y `Activate` cambian el estado y fallan si se repiten. Sin domain events |
| `tests/Dispensario.Application.UnitTests/Common/TestPatients.cs` | Builder de pacientes de prueba (como `TestDoctors`) |
| `tests/Dispensario.Application.UnitTests/Persistence/Patients/PatientPersistenceTests.cs` | Ida y vuelta de un `Patient` con todos los campos (dirección, tipo de sangre como texto, `BirthDate`) y auditoría; ida y vuelta sin opcionales (dirección null sigue null); documento repetido → `DbUpdateException`; mismo número con otro tipo se guarda |
| `tests/Dispensario.Application.UnitTests/Patients/BloodTypeNotationTests.cs` | Los ocho valores van y vuelven; `"a+"`, `"A +"`, `"APositive"` y `""` no se aceptan |
| `tests/Dispensario.Application.UnitTests/Patients/CreatePatientCommandValidatorTests.cs` | Documento vacío o de 21 caracteres, nombre vacío, género fuera del enum, email inválido, dirección sin calle o sin ciudad, `bloodType` `"X+"` y alergias de 1001 caracteres fallan; un comando mínimo (sin opcionales) y uno completo pasan. Incluye `UpdatePatientCommandValidator`: `PatientId` vacío falla |
| `tests/Dispensario.Application.UnitTests/Patients/CreatePatientHandlerTests.cs` | Crea el paciente con `BloodType` traducido y dirección; documento repetido → `ConflictException`; mismo número con otro tipo pasa; fecha futura según el `TimeProvider` falso → `DomainException` |
| `tests/Dispensario.Application.UnitTests/Patients/UpdatePatientHandlerTests.cs` | Cambia los campos y el documento; mantener su propio documento no da conflicto; documento de otro paciente → `ConflictException`; inexistente → `NotFoundException`; `address` null borra la dirección; edita un paciente inactivo |
| `tests/Dispensario.Application.UnitTests/Patients/DeactivatePatientHandlerTests.cs` | Desactiva; ya inactivo → `DomainException`; inexistente → `NotFoundException`. Incluye `ActivatePatientHandler`: activa; ya activo → `DomainException`; inexistente → `NotFoundException` |
| `tests/Dispensario.Application.UnitTests/Patients/GetPatientsHandlerTests.cs` | Paginación (`Count` total y tamaño de página); filtro `IsActive`; `Search` por apellido y por número de documento sin distinguir mayúsculas; `%` en `search` se trata como literal; orden por apellido; `BloodType` sale como `"O+"`; `PageSize = 0` o `101` → `BadRequestException` |
| `tests/Dispensario.Application.UnitTests/Patients/GetPatientByIdHandlerTests.cs` | Devuelve el `PatientDto` completo con `AddressDto` y `CreatedAtUtc`; sin dirección → `Address` null; inexistente → `NotFoundException` |
| `tests/Dispensario.Application.UnitTests/Patients/GetPatientByDocumentHandlerTests.cs` | Encuentra por tipo y número con espacios alrededor; mismo número con otro tipo → `NotFoundException`; devuelve un inactivo; número en blanco → `BadRequestException` |
| `tests/Dispensario.Api.IntegrationTests/Common/PatientRequests.cs` | Helper que crea un paciente válido y devuelve su ID |
| `tests/Dispensario.Api.IntegrationTests/Patients/PatientsEndpointsTests.cs` | Como `Receptionist`: alta → 201 con `Location` y `GET /api/patients/{id}` lo devuelve con `bloodType = "O+"` y la dirección; documento repetido → 409; `bloodType` `"X+"` → 400 con errores de validación; `gender` desconocido → 400; nacimiento futuro → 400; `GET /api/patients?search=…` lo lista; `GET /api/patients/by-document?type=NationalId&number=…` lo devuelve y con otro número → 404; `PUT` → 204 (cambia el documento) y con el documento de otro paciente → 409; desactivar → 204 y `?isActive=false` lo lista; activar → 204; `GET` de un ID inexistente → 404. Como Admin: alta → 201. Como `Doctor`: los `GET` → 200 y los de escritura → 403. Sin token → 401 |

- Convenciones de `TESTING.md`: `Method_Scenario_ExpectedResult`, Arrange / Act / Assert y Shouldly.

## Implementation plan

1. Crear `PatientId`, `Gender`, `BloodType` y `Patient` en `Dispensario.Domain/Patients/`, con `PatientTests`. Verificar: `dotnet test` en verde.
2. Crear `PatientConfiguration` y añadir `DbSet<Patient>` a `IApplicationDbContext` y `DispensarioDbContext`. Crear `TestPatients` y `PatientPersistenceTests`. Verificar: `dotnet test` en verde y, tras borrar `dispensario.db`, `dotnet run` crea la tabla `Patients`.
3. Crear `BloodTypeNotation` con `BloodTypeNotationTests`, y `CreatePatient` (command, validador, handler y endpoint), con `CreatePatientCommandValidatorTests`, `CreatePatientHandlerTests`, el helper `PatientRequests` y los casos de alta de `PatientsEndpointsTests`. Verificar: `dotnet test` en verde y alta desde Scalar.
4. Crear `PatientDto`, `AddressDto`, `PatientsMappingConfig`, `GetPatientById` y `GetPatients` (queries, handlers y endpoints), con `GetPatientByIdHandlerTests`, `GetPatientsHandlerTests` y sus casos de integración. Verificar: `dotnet test` en verde.
5. Crear `GetPatientByDocument` (query, handler y endpoint), con `GetPatientByDocumentHandlerTests` y sus casos de integración. Verificar: `dotnet test` en verde.
6. Crear `UpdatePatient` (command, validador, handler y endpoint), con `UpdatePatientHandlerTests`, los casos de `UpdatePatientCommandValidator` y sus casos de integración. Verificar: `dotnet test` en verde.
7. Crear `DeactivatePatient` y `ActivatePatient` (commands, validadores, handlers y endpoints), con `DeactivatePatientHandlerTests` y sus casos de integración, incluidos los 403 del rol `Doctor`. Verificar: `dotnet test` en verde.
8. Actualizar la documentación:
   - `README.md` sección **Endpoints**: filas nuevas en la tabla general y apartado **Patients** con cada ruta: acceso, ejemplo de petición y respuesta, y tabla de códigos de error. Indicar la notación de `bloodType` y que el número de documento distingue mayúsculas.
   - `docs/guides/DOMAIN.md` §4 Patients: un único `Update(...)` en lugar de `UpdatePersonalData`/`UpdateMedicalData`, documento editable y único, `Activate()`, `BirthDate` no futura con `today` desde el handler, `Email`/`Phone`/`Address`/`BloodType`/`Allergies` opcionales, máximo de `Allergies`, sin eventos. §5: carpeta `Patients/` sin `Events/`.
   - `docs/guides/APPLICATION.md` §2: estado de `IApplicationDbContext` (hoy `Users`, `Doctors` y `Patients`) y tabla "Módulo Patients" con los casos de uso. El ejemplo de `CreatePatientHandler` compara `Type` y `Number` por separado.
   - `docs/guides/INFRASTRUCTURE.md`: tabla `Patients` y su índice único; se quita el índice de `LastName`.
   - `docs/guides/API.md` §1: `Endpoints/Patients/` con sus endpoints (escritura `Reception`, lecturas autenticadas).
   - `CLAUDE.md`: "Estado actual" con el CRUD de pacientes (feature 11); "Detalles no obvios" con la notación `"A+"` traducida por `BloodTypeNotation` (enum en dominio y BD), `today` pasado al dominio para validar `BirthDate`, la ruta `by-document` y el documento editable.
   - `docs/PLAN.md`: la fila 11 apunta a `specs/11-patients-crud.md`.

## Acceptance criteria

- [x] `dotnet build Dispensario.slnx` termina sin errores y sin warnings nuevos respecto a SPEC 10.
- [x] `dotnet test Dispensario.slnx` termina con éxito e incluye los tests de este spec y los anteriores.
- [x] Tras borrar `dispensario.db`, `dotnet run` crea la tabla `Patients` con el índice único de (`Document_Type`, `Document_Number`).
- [x] `POST /api/patients` como `Receptionist` o Admin devuelve 201 con `Location: /api/patients/{id}`; solo documento, nombre, apellido, nacimiento y género son obligatorios.
- [x] Un documento ya usado (mismo tipo y número) devuelve 409 en el alta y en la edición; el mismo número con otro tipo se acepta.
- [x] Una fecha de nacimiento futura devuelve 400; la de hoy se acepta.
- [x] `bloodType` acepta solo `A+`, `A-`, `B+`, `B-`, `AB+`, `AB-`, `O+` y `O-` (otro valor → 400 con errores de validación), se guarda como nombre del enum y se devuelve en notación clínica.
- [x] `PUT /api/patients/{id}` responde 204, puede cambiar el documento, y un opcional null (`email`, `phone`, `address`, `bloodType`, `allergies`) borra el valor actual.
- [x] `GET /api/patients` devuelve `pageIndex`, `pageSize`, `count` y `data`, filtra por `search` (nombre, apellido, número de documento) e `isActive`, y responde 400 con `pageSize` fuera de 1–100.
- [x] `GET /api/patients/{id}` devuelve el paciente con `address` (o `null`) y `createdAtUtc`, y 404 si no existe.
- [x] `GET /api/patients/by-document?type=…&number=…` devuelve el paciente (también inactivo) y 404 si no existe.
- [x] `POST /api/patients/{id}/deactivate` y `/activate` responden 204, y 400 si el paciente ya está en ese estado.
- [x] Los endpoints de escritura responden 403 al rol `Doctor`; las tres lecturas responden 200 a cualquier rol; todos responden 401 sin token.
- [x] Todos los errores salen como `application/problem+json`.
- [x] Las siete rutas nuevas aparecen en `/openapi/v1.json` con tag `Patients`, nombre y resumen.
- [x] La sección **Endpoints** del `README.md` documenta las siete rutas nuevas con acceso, petición, respuesta y códigos de error.
- [x] `DOMAIN.md`, `APPLICATION.md`, `INFRASTRUCTURE.md`, `API.md` y `CLAUDE.md` describen el módulo Patients tal como se implementó, y `docs/PLAN.md` fila 11 apunta a `specs/11-patients-crud.md`.

## Decisions

- **Yes:** escritura con la política `Reception` (Admin y Receptionist); lecturas para cualquier usuario autenticado, porque el doctor necesita ver al paciente. Decisión del usuario.
- **No:** que un `Doctor` edite los datos médicos con un endpoint propio. Descartado por el usuario.
- **Yes:** un único `PUT /api/patients/{id}` y un único `Patient.Update(...)` que reemplaza datos personales y médicos. Más simple y simétrico con Doctors. Decisión del usuario.
- **No:** `UpdatePersonalData` y `UpdateMedicalData` separados, como proponía `DOMAIN.md`. Descartado por el usuario; se actualiza la guía.
- **Yes:** el documento es editable y un choque con otro paciente da 409. Los errores de tipeo en recepción son frecuentes. Decisión del usuario.
- **No:** documento inmutable como `Identification` en Users. Descartado por el usuario.
- **Yes:** `Deactivate` y `Activate`, simétrico con Users y Doctors; reactivar evita duplicar al paciente, que el documento único impediría. Decisión del usuario.
- **Yes:** obligatorios documento, nombre, apellido, nacimiento y género; el resto opcional. Decisión del usuario.
- **Yes:** `BirthDate` solo no futura, sin límite inferior. La fecha de hoy la pasa el handler con `TimeProvider`. Decisión del usuario.
- **Yes:** el número de documento solo se recorta, como hoy en el VO `Document` (spec 06). No toca Users. Decisión del usuario.
- **No:** normalizar el documento a mayúsculas. Cambiaría también Users y sus datos. Descartado por el usuario.
- **Yes:** `GET /api/patients/by-document?type=&number=` con ruta propia, que devuelve un `PatientDto` o 404. Decisión del usuario.
- **No:** segmentos de ruta (`/document/{type}/{number}`) o solo filtro en el listado. Descartados por el usuario.
- **Yes:** `GET /api/patients` con `search` (nombre, apellido, número de documento) e `isActive`, paginado como Users y Doctors. Decisión del usuario.
- **No:** filtro por género. Descartado por el usuario.
- **Yes:** `bloodType` en notación clínica (`"A+"`) en la Api. Decisión del usuario.
- **Yes:** la traducción vive en `Application` (`BloodTypeNotation`); el dominio solo conoce el enum y la BD guarda su nombre. Un valor inválido da `ValidationErrors`. Decisión del usuario.
- **No:** `[JsonStringEnumMemberName("A+")]` en el enum del dominio. Haría depender el dominio de System.Text.Json. Descartado por el usuario.
- **Yes:** dirección como objeto anidado opcional; `null` en el `PUT` la borra. Se mapea con `ComplexProperty` opcional. Decisión del usuario.
- **No:** domain events en Patients (`PatientRegisteredDomainEvent`). Nadie reacciona hoy (`DOMAIN.md` §2 regla 7). Decisión del usuario.
- **Yes:** `Update` se permite sobre pacientes inactivos, igual que en Doctors.
- **Yes:** `Allergies` texto libre de hasta 1000 caracteres; en blanco se guarda como null.
- **Yes:** `GetPatientByDocument` devuelve también pacientes inactivos: recepción debe encontrarlos para reactivarlos.
- **No:** índice sobre `LastName`. `ComplexProperty` no admite índices y el volumen de v1 no lo necesita.
- **Yes:** proyección con Mapster (`ProjectToType<PatientDto>`): la traducción de `BloodType` se evalúa en cliente en la proyección final.

## Risks

| Risk | Mitigation |
| --- | --- |
| `ComplexProperty` opcional (`Address?`) puede no estar soportado o no distinguir null en lectura | El paso 2 lo comprueba con `PatientPersistenceTests` (ida y vuelta con y sin dirección). Si falla, se mapea con `OwnsOne` en la misma tabla, sin cambiar las columnas. |
| `BloodTypeNotation.ToNotation` dentro de `ProjectToType` puede no traducirse a SQL | EF evalúa en cliente la proyección final. Si falla, el paso 4 mapea en memoria la página ya leída (máximo 100 filas). |
| `DateOnly` en SQLite se guarda como TEXT | EF Core SQLite lo soporta y compara bien en formato `yyyy-MM-dd`; aquí no se filtra ni se ordena por fecha. |
| Documento repetido en dos altas simultáneas | El índice único lanza `DbUpdateException` → 500. Muy improbable; aceptado en v1, igual que en Doctors. |
| `ab123` y `AB123` de pasaporte se guardan como pacientes distintos | Aceptado en v1 (decisión del usuario). Documentado en `README.md`. |
| `LIKE` en SQLite solo ignora mayúsculas en ASCII: "Núñez" y "NÚÑEZ" no coinciden en `search` | Aceptado en v1, igual que en `GetUsers` y `GetDoctors`. |
| La fecha de hoy es la UTC: entre medianoche local y UTC el día puede variar uno | Afecta solo a nacimientos registrados el mismo día. Se revisa cuando llegue `Clinic:TimeZone` (fila 12). |
| El esquema cambia y una `dispensario.db` local antigua no tiene la tabla `Patients` | Borrar `dispensario.db` antes de `dotnet run` (`EnsureCreated` hasta la fila 17). |

## What is **not** in this spec

- Historia clínica del paciente y comprobación de paciente activo al agendar.
- Edición de datos médicos por el `Doctor`.
- Domain events en Patients.
- Documento en mayúsculas y nuevos tipos de documento.
- Filtro por género y límite inferior de la fecha de nacimiento.
- Borrado y fusión de pacientes, y migraciones.

Cada uno de ellos, si llega, va en su propio spec.
