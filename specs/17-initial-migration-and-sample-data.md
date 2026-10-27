# SPEC 17 — Migración inicial y datos de ejemplo

> **Status:** Approved
> **Depends on:** SPEC 04, SPEC 07, SPEC 10, SPEC 11, SPEC 15
> **Date:** 2026-10-27
> **Objective:** Sustituir `EnsureCreatedAsync` por la migración `InitialCreate` aplicada con `MigrateAsync` y sembrar en Development un conjunto fijo de doctores, horarios, pacientes y usuarios de ejemplo.

## Por qué este spec existe

Hasta ahora el esquema sale de `EnsureCreatedAsync`, que no actualiza una base ya creada: cada cambio del modelo obliga a borrar `dispensario.db`.
Con el modelo de v1 completo (filas 06 a 15), se congela en una migración inicial y a partir de aquí cada cambio llega como migración.
Los datos de ejemplo permiten probar la Api desde Scalar sin dar de alta a mano doctores, horarios y pacientes.
Es un cambio de una sola vez para las bases locales: una `dispensario.db` creada con `EnsureCreated` no tiene `__EFMigrationsHistory` y `MigrateAsync` falla sobre ella, así que hay que borrarla una vez.

## Scope

**In:**

- Paquete `Microsoft.EntityFrameworkCore.Design` 10.0.14 en `Directory.Packages.props` y referenciado desde `Dispensario.Api` (proyecto de arranque de `dotnet ef`) con `PrivateAssets="all"`.
- Migración `InitialCreate` generada con `dotnet ef migrations add` en `Dispensario.Infrastructure/Data/Migrations/`, con su `DispensarioDbContextModelSnapshot`.
- `InitialiseDatabaseAsync` usa `MigrateAsync` en lugar de `EnsureCreatedAsync` y, tras el seed del Admin, ejecuta `SampleDataSeeder`.
- `SampleDataSeedOptions` (sección `Seed:SampleData`: `Enabled` y `Password`) y `SampleDataSeeder` en `Dispensario.Infrastructure/Data/Seed/`, registrados en `AddInfrastructure`.
- Datos de ejemplo fijos: 3 doctores con horario semanal, 5 pacientes, un `Receptionist` y un usuario `Doctor` vinculado a su ficha.
- `appsettings.json` con la sección `Seed:SampleData` (`Enabled: true`, `Password: ""`).
- `DispensarioApiFactory` apaga los datos de ejemplo (`Seed:SampleData:Enabled=false`); los tests de integración pasan a crear el esquema con la migración (mismo camino que Development).
- `DispensarioTestDb` (tests de persistencia) crea el esquema con `Migrate()` en lugar de `EnsureCreated()`; sus tests de esquema `EnsureCreated_*` se renombran a `Migrate_*`.
- Tests nuevos: `MigrationsTests` (sin cambios de modelo pendientes, migración aplicada) y `SampleDataSeederTests`.
- Documentación: `docs/guides/INFRASTRUCTURE.md`, `docs/guides/API.md`, `docs/guides/TESTING.md`, `CLAUDE.md`, `README.md` y la fila 17 de `docs/PLAN.md`.

**Out of scope (for future specs):**

- Aplicar migraciones al arrancar fuera de Development: en otros entornos se aplica con `dotnet ef database update` o con un script SQL.
- Detectar o borrar automáticamente una `dispensario.db` antigua creada con `EnsureCreated`.
- Citas o consultas de ejemplo.
- Usuarios de ejemplo para los doctores de Pediatría y Cardiología, y un segundo Admin.
- Un manifiesto local de herramientas (`.config/dotnet-tools.json`) para `dotnet-ef`.
- Cambios de dominio, de Application o de endpoints.
- Emitir `ConsultationClosedDomainEvent` y su handler (pendiente de la fila 16) y los tests E2E (fila 18).

## Data model

El modelo de EF no cambia: la migración `InitialCreate` reproduce el esquema que hoy genera `EnsureCreated`.
Tablas: `Users`, `Doctors`, `DoctorScheduleBlocks`, `Patients`, `Appointments`, `Consultations` (con las columnas e índices de `INFRASTRUCTURE.md` §3), más `__EFMigrationsHistory`, que crea EF.

### Migraciones (`Dispensario.Infrastructure/Data/Migrations/`, nuevo)

```
Data/Migrations/
  <timestamp>_InitialCreate.cs
  <timestamp>_InitialCreate.Designer.cs
  DispensarioDbContextModelSnapshot.cs
```

- Se generan con el comando ya documentado; no se escriben a mano:
  `dotnet ef migrations add InitialCreate -p src/Services/Dispensario/Dispensario.Infrastructure -s src/Services/Dispensario/Dispensario.Api -o Data/Migrations`.
- Se revisa la migración generada antes de aplicarla: tablas, índices únicos, FK `DoctorScheduleBlocks.DoctorId` en cascada y **ninguna** FK entre módulos.
- `dotnet ef` resuelve el `DbContext` con el host real de la Api (`Program.cs`) y su `ConnectionStrings:Database`; no hay `IDesignTimeDbContextFactory`.

### `InitialiseDatabaseAsync` (`Data/Extensions/DatabaseExtensions.cs`, cambio)

Orden dentro de un mismo scope:
1. `dbContext.Database.MigrateAsync(cancellationToken)`.
2. `DatabaseSeeder.SeedAsync` (Admin, sin cambios).
3. `SampleDataSeeder.SeedAsync` (nuevo).

Sigue llamándose solo en Development desde `Program.cs`.

### `SampleDataSeedOptions` (`Data/Seed/SampleDataSeedOptions.cs`, nuevo)

```csharp
// Sección Seed:SampleData; sin ValidateOnStart, como AdminSeedOptions
public sealed class SampleDataSeedOptions
{
    public const string SectionName = "Seed:SampleData";

    // true por defecto: en Development siempre se siembra salvo que se apague (los tests de integración lo apagan)
    public bool Enabled { get; init; } = true;

    // En user-secrets; vacía → se siembran doctores y pacientes, pero no los usuarios
    public string Password { get; init; } = string.Empty;
}
```

`appsettings.json`:
```json
"Seed": {
  "Admin": { "...": "sin cambios" },
  "SampleData": {
    "Enabled": true,
    "Password": ""
  }
}
```

### `SampleDataSeeder` (`Data/Seed/SampleDataSeeder.cs`, nuevo)

Dependencias: `DispensarioDbContext`, `IPasswordHasher`, `TimeProvider`, `IOptions<SampleDataSeedOptions>` y `ILogger<SampleDataSeeder>`. Se registra como `Scoped` en `AddInfrastructure` junto con `AddOptions<SampleDataSeedOptions>().BindConfiguration(...)`.

Reglas de `SeedAsync`:
- `Enabled = false` → no hace nada ni escribe en el log.
- Si ya existe algún doctor **o** algún paciente → no hace nada (idempotencia por base "no vacía"; no mezcla datos reales con los de ejemplo).
- Crea los doctores con `Doctor.Create` y su horario con `SetWeeklySchedule`, y los pacientes con `Patient.Create` (`today` = fecha UTC de `TimeProvider`).
- Si `Password` está vacía, registra el warning `Sample users skipped: 'Seed:SampleData:Password' is empty.` y no crea usuarios.
- Con contraseña, crea los dos usuarios con `User.Create` y el hash de `IPasswordHasher`; el usuario `Doctor` se vincula con `LinkDoctor` al doctor de Medicina General. Un usuario cuyo email ya existe se omite (no falla el arranque por el índice único).
- Todo se guarda con un único `SaveChangesAsync`.
- Al terminar registra `Sample data seeded: {Doctors} doctors, {Patients} patients, {Users} users.` (Information).
- Los datos son válidos por construcción: un `DomainException` aquí es un error de programación, no de configuración.

### Datos de ejemplo

Doctores (todos activos, sin teléfono):

| Nombre | Matrícula | Especialidad | Email | Duración | Horario (hora local) |
|---|---|---|---|---|---|
| Ana Torres | `MG-0001` | Medicina General | `doctor@dispensario.local` | 30 | Lunes a viernes 08:00–12:00 y 14:00–18:00 |
| Pedro Salazar | `PED-0002` | Pediatría | `pediatria@dispensario.local` | 20 | Lunes a viernes 08:00–13:00 |
| Lucía Benítez | `CAR-0003` | Cardiología | `cardiologia@dispensario.local` | 40 | Martes y jueves 09:00–13:00 |

Pacientes (todos activos, documento `NationalId`):

| Documento | Nombre | Nacimiento | Género | Sangre | Otros |
|---|---|---|---|---|---|
| `1710000001` | Carlos Mendoza | 1985-03-14 | `Male` | `OPositive` | Dirección: `Av. Amazonas N24-03`, `Quito`, `Pichincha`; teléfono `0991234567` |
| `1710000002` | María Vera | 1992-07-08 | `Female` | `APositive` | Alergias: `Penicilina`; email `maria.vera@example.com` |
| `0910000003` | Luis Andrade | 2018-11-20 | `Male` | — | — |
| `0910000004` | Sofía Cedeño | 1960-01-30 | `Female` | `BNegative` | — |
| `1310000005` | Jorge Paredes | 2001-05-05 | `Male` | `ABPositive` | — |

Usuarios (solo con `Seed:SampleData:Password`, activos, sin teléfono):

| Email | Nombre | Documento | Rol | Doctor vinculado |
|---|---|---|---|---|
| `recepcion@dispensario.local` | Rosa Molina | `NationalId` `1700000010` | `Receptionist` | — |
| `doctor@dispensario.local` | Ana Torres | `NationalId` `1700000011` | `Doctor` | Ana Torres (`MG-0001`) |

Ningún documento ni email choca con el Admin por defecto (`admin@dispensario.local`, `0000000000`).

### Tests

`tests/Dispensario.Application.UnitTests/Persistence/MigrationsTests.cs` (nuevo):
- `Model_HasNoPendingChanges`: `Database.HasPendingModelChanges()` es `false` (falla si alguien cambia el modelo sin migración).
- `Migrate_OnEmptyDatabase_AppliesInitialCreate`: tras migrar, `GetAppliedMigrationsAsync` contiene una sola migración que termina en `_InitialCreate`.

`tests/Dispensario.Application.UnitTests/Persistence/Seed/SampleDataSeederTests.cs` (nuevo, como `DatabaseSeederTests`):
- `SeedAsync_OnEmptyDatabase_CreatesDoctorsWithSchedules`: 3 doctores con las matrículas, duraciones y número de bloques de la tabla.
- `SeedAsync_OnEmptyDatabase_CreatesPatients`: 5 pacientes con sus documentos.
- `SeedAsync_WithPassword_CreatesUsersAndLinksDoctor`: el `Receptionist` y el `Doctor`, este con `DoctorId` del doctor `MG-0001`; la contraseña verifica con el hasher.
- `SeedAsync_WithEmptyPassword_SkipsUsersAndLogsWarning`: doctores y pacientes sí, usuarios no, y el warning exacto.
- `SeedAsync_WhenDisabled_DoesNothing`.
- `SeedAsync_WhenDoctorsExist_DoesNothing` y `SeedAsync_WhenPatientsExist_DoesNothing`.
- `SeedAsync_RunTwice_DoesNotDuplicate`.
- `SeedAsync_WhenUserEmailExists_SkipsThatUser`.

Tests existentes:
- `DispensarioTestDb` usa `context.Database.Migrate()`; los tests `EnsureCreated_*` de `AppointmentPersistenceTests`, `ConsultationPersistenceTests`, `DoctorPersistenceTests`, `PatientPersistenceTests` y `UserPersistenceTests` se renombran a `Migrate_*` sin cambiar sus asertos: así verifican el esquema de la migración.
- `TestDb` (contexto genérico de BuildingBlocks) sigue con `EnsureCreated`: no tiene migraciones.
- Los tests de integración no cambian salvo `DispensarioApiFactory`, que añade `builder.UseSetting("Seed:SampleData:Enabled", "false")`.

## Implementation plan

1. Añadir `Microsoft.EntityFrameworkCore.Design` 10.0.14 a `Directory.Packages.props` y su `PackageReference` (con `PrivateAssets="all"`) a `Dispensario.Api.csproj`. `dotnet build Dispensario.slnx` sin errores.
2. Generar `InitialCreate` con `dotnet ef migrations add` en `Data/Migrations` y revisarla (tablas, índices, FK en cascada de `DoctorScheduleBlocks`, sin FKs entre módulos). Compila.
3. Cambiar `InitialiseDatabaseAsync` a `MigrateAsync` (y su comentario y el de `Program.cs`). Cambiar `DispensarioTestDb` a `Migrate()` y renombrar los tests `EnsureCreated_*` a `Migrate_*`. `dotnet test Dispensario.slnx` en verde.
4. Añadir `MigrationsTests`. En verde.
5. Añadir `SampleDataSeedOptions`, la sección `Seed:SampleData` en `appsettings.json` y su registro en `AddInfrastructure`; `DispensarioApiFactory` pone `Seed:SampleData:Enabled=false`.
6. Añadir `SampleDataSeeder` con los doctores, horarios y pacientes, y llamarlo desde `InitialiseDatabaseAsync` tras `DatabaseSeeder`.
7. Añadir los usuarios de ejemplo (contraseña, vínculo con el doctor y omisión por email existente).
8. Añadir `SampleDataSeederTests`. `dotnet test Dispensario.slnx` en verde.
9. Prueba manual: borrar `dispensario.db`, `dotnet run` con `Seed:SampleData:Password` en user-secrets, ver la tabla `__EFMigrationsHistory` con `InitialCreate`, hacer login como `doctor@dispensario.local` y pedir `GET /api/doctors/{id}/available-slots` de un día laborable futuro.
10. Actualizar la documentación:
    - `INFRASTRUCTURE.md`: estructura (`Migrations/` con `InitialCreate`, `SampleDataSeeder`), §7 (MigrateAsync, datos de ejemplo, borrar la base una vez) y §9 (paquete Design, fuera de Development se migra con `dotnet ef database update`).
    - `API.md`: comentario de `InitialiseDatabaseAsync`.
    - `TESTING.md`: `DispensarioTestDb` con `Migrate()`, la factory con la migración y los datos de ejemplo apagados, `MigrationsTests`.
    - `CLAUDE.md`: estado actual (feature 17), el detalle de `EnsureCreated` sustituido por migraciones (borrar `dispensario.db` una vez; cada cambio de modelo, una migración nueva), datos de ejemplo y el comando de `Seed:SampleData:Password`.
    - `README.md`: puesta en marcha (borrar la base antigua una vez, `Seed:SampleData:Password`, usuarios de ejemplo). La sección **Endpoints** no cambia.
    - `PLAN.md`: anotar `specs/17-initial-migration-and-sample-data.md` en la fila 17.

## Acceptance criteria

- [ ] `Dispensario.Infrastructure/Data/Migrations/` contiene `*_InitialCreate.cs`, `*_InitialCreate.Designer.cs` y `DispensarioDbContextModelSnapshot.cs`, generados con `dotnet ef`.
- [ ] La migración crea `Users`, `Doctors`, `DoctorScheduleBlocks`, `Patients`, `Appointments` y `Consultations` con los índices de `INFRASTRUCTURE.md` §3, la FK en cascada de `DoctorScheduleBlocks` y ninguna FK entre módulos.
- [ ] `Model_HasNoPendingChanges` está en verde.
- [ ] Ningún archivo de `src/` llama a `EnsureCreated` ni a `EnsureCreatedAsync`.
- [ ] Los tests `Migrate_*` de persistencia (antes `EnsureCreated_*`) pasan sobre el esquema migrado sin cambiar sus asertos.
- [ ] Con `dispensario.db` borrada, `dotnet run` crea la base con `__EFMigrationsHistory` que contiene `InitialCreate`.
- [ ] Un segundo `dotnet run` sobre la misma base arranca sin errores y sin duplicar el Admin ni los datos de ejemplo.
- [ ] Con `Seed:SampleData:Password` configurada, tras el arranque hay 3 doctores con horario, 5 pacientes, `recepcion@dispensario.local` (`Receptionist`) y `doctor@dispensario.local` (`Doctor`) vinculado al doctor `MG-0001`, y ambos pueden hacer login.
- [ ] Con `Seed:SampleData:Password` vacía, se siembran doctores y pacientes, no se crean usuarios y el log muestra `Sample users skipped: 'Seed:SampleData:Password' is empty.`.
- [ ] Con `Seed:SampleData:Enabled=false` no se siembran datos de ejemplo.
- [ ] Si ya hay algún doctor o paciente, `SampleDataSeeder` no crea nada.
- [ ] Los tests de integración arrancan con la migración y sin datos de ejemplo.
- [ ] `dotnet build Dispensario.slnx` sin errores ni advertencias nuevas.
- [ ] `dotnet test Dispensario.slnx` en verde.
- [ ] `INFRASTRUCTURE.md`, `API.md`, `TESTING.md`, `CLAUDE.md`, `README.md` y la fila 17 de `PLAN.md` reflejan las migraciones y los datos de ejemplo.

## Decisions

- **Yes:** una sola migración `InitialCreate` con todo el modelo de v1. Es lo que pide la fila 17.
- **Yes:** `MigrateAsync` solo en Development, como hoy `EnsureCreated`. Decisión del usuario.
- **No:** migrar al arrancar en todos los entornos. Descartado por el usuario: abre el tema de despliegues concurrentes; fuera de Development se usa `dotnet ef database update`.
- **Yes:** una `dispensario.db` antigua se borra a mano una vez, y se documenta. Decisión del usuario.
- **No:** detectar la base antigua y fallar con un mensaje claro. Descartado: código permanente para un caso de una sola vez.
- **No:** borrarla automáticamente. Descartado: destructivo.
- **Yes:** `dotnet ef` usa el host de la Api con `Microsoft.EntityFrameworkCore.Design` en la Api. Decisión del usuario: coincide con los comandos ya documentados.
- **No:** `IDesignTimeDbContextFactory` en Infrastructure. Descartado por el usuario.
- **Yes:** los tests de integración y los de persistencia crean el esquema con la migración, y un test falla si hay cambios de modelo sin migración. Decisión del usuario.
- **No:** mantener `EnsureCreated` en los tests. Descartado: no probaría la migración.
- **Yes:** renombrar los tests `EnsureCreated_*` a `Migrate_*`. Su nombre dejaría de describir lo que comprueban.
- **Yes:** datos de ejemplo siempre en Development, apagables con `Seed:SampleData:Enabled`. Decisión del usuario.
- **Yes:** la factory de integración los apaga. Decisión del usuario: los tests siguen deterministas.
- **Yes:** doctores con horario, pacientes y dos usuarios (`Receptionist` y `Doctor` vinculado). Decisión del usuario, con el conjunto concreto propuesto.
- **No:** citas de ejemplo. Descartado por el usuario: dependen de la fecha actual y son la parte más frágil.
- **Yes:** contraseña de los usuarios de ejemplo en `Seed:SampleData:Password` (user-secrets); vacía, se omiten los usuarios con un warning. Decisión del usuario.
- **No:** reutilizar `Seed:Admin:Password` o una contraseña fija en el código. Descartado por el usuario.
- **Yes:** idempotencia por base "no vacía": se siembra todo solo si no hay doctores ni pacientes. Decisión del usuario.
- **No:** idempotencia por clave natural, uno a uno. Descartado por el usuario: más código y casos.
- **Yes:** un usuario de ejemplo cuyo email ya existe se omite. Evita que el arranque falle por el índice único si alguien se registró antes con ese email.
- **Yes:** `SampleDataSeeder` separado de `DatabaseSeeder`. Decisión del usuario.
- **Yes:** el seeder usa los factories del dominio (`Doctor.Create`, `SetWeeklySchedule`, `Patient.Create`, `User.Create`, `LinkDoctor`), no `HasData`. `HasData` metería los datos de ejemplo en la migración y los llevaría a cualquier entorno.

## Risks

| Risk | Mitigation |
| --- | --- |
| Una `dispensario.db` antigua hace fallar el arranque con `table "Users" already exists` | `README.md`, `CLAUDE.md` e `INFRASTRUCTURE.md` dicen que hay que borrarla una vez. |
| La migración generada no coincide con el esquema que verificaban los tests de `EnsureCreated` | Los tests de esquema pasan a ejecutarse sobre la migración (`Migrate_*`) sin cambiar sus asertos. |
| Alguien cambia el modelo y olvida la migración | `Model_HasNoPendingChanges` falla; EF 10 además avisa con `PendingModelChangesWarning` al migrar. |
| `dotnet-ef` global (10.0.11) es más antiguo que el runtime de EF (10.0.14) | Funciona con un aviso de versión; actualizar la herramienta es opcional y queda fuera del spec. |
| `dotnet ef` necesita construir el host de la Api, que valida `Jwt:Key` al arrancar | La validación (`ValidateOnStart`) ocurre al iniciar el host, no al construirlo; si falla, se configura `Jwt:Key` en user-secrets como ya pide el `README.md`. |
| Los datos de ejemplo chocan con usuarios o pacientes creados a mano | Con algún doctor o paciente no se siembra nada; los usuarios con email existente se omiten. |

## What is **not** in this spec

- Migraciones al arrancar fuera de Development.
- Detección o borrado automático de una base antigua.
- Citas o consultas de ejemplo.
- Más usuarios de ejemplo.
- Manifiesto local de herramientas para `dotnet-ef`.
- Cambios de dominio, Application o endpoints.
- El evento de consulta cerrada y los tests E2E.

Cada uno de ellos, si llega, va en su propio spec.
