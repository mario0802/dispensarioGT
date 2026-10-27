# Plan / Hoja de ruta — Dispensario

Desarrollo incremental con **SDD** usando las skills `/spec` y `/spec-impl` (de Fernando Herrera, instaladas en `.agents/skills/`).
Este documento es el **backlog**: cada fila es candidata a un spec. El spec real lo crea `/spec` en `specs/NN-slug.md` y su estado vive en ese archivo.

## Flujo
```
/spec <feature>        → preguntas de aclaración → specs/NN-slug.md en estado Draft (sin código)
(revisión humana)      → el usuario cambia el estado a Approved a mano
/spec-impl NN-slug     → crea la rama spec-NN-slug e implementa paso a paso, con pausa tras cada paso para revisar el diff
(verificación)         → criterios de aceptación OK → estado Implemented → commit y merge (los hace el usuario)
```
Estados de un spec: `Draft` → `In review` → `Approved` → `Implemented` (o `Obsolete`).
Configuración: `specs/.spec-config.yml` (`AutoCreateBranch`), que crea `/spec` la primera vez.

**Requisito previo:** `/spec-impl` usa git (ramas por spec). Hay que ejecutar `git init` y hacer un primer commit antes de implementar el primer spec.

Al crear un spec con `/spec`, anota su archivo en la columna **Spec** de este backlog. `/spec` numera de forma secuencial, así que el orden propuesto abajo da la numeración esperada.

## Alcance v1
- **Users / Auth**: login con JWT, gestión de usuarios por el Admin y roles `Admin`, `Doctor`, `Receptionist`.
- **Doctors**: alta y edición, especialidad, matrícula, duración de la cita y horario semanal.
- **Patients**: registro con documento único, datos personales, tipo de sangre y alergias.
- **Appointments**: agendar en slots según el horario del doctor y sin cruces; estados y reprogramación.
- **Consultations**: con o sin cita; motivo, diagnóstico y notas; al cerrarla se completa la cita.

**Fuera de v1:** signos vitales, recetas, exámenes, farmacia/inventario, facturación, frontend, notificaciones.

## Backlog (orden sugerido)

### Fase 0 — Fundaciones
| # | Feature | Contenido | Spec |
|---|---|---|---|
| 01 | Solución y BuildingBlocks | `Dispensario.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`. BuildingBlocks a net10 dentro de la solución y ajustes de paquetes (ver ARCHITECTURE §5) | `specs/01-solution-and-buildingblocks-net10.md` |
| 02 | Proyectos del servicio y BuildingBlocks.Domain | Los cuatro proyectos `Dispensario.*` y los tres de tests con sus referencias. Nuevo `BuildingBlocks.Domain` y ajustes de `CustomExceptionHandler` y `LoggingBehavior` (ver ARCHITECTURE §5). Añadir `*.db`, `*.db-shm` y `*.db-wal` a `.gitignore` | `specs/02-service-projects-and-buildingblocks-domain.md` |
| 03 | Abstracciones de dominio | `Entity<TId>`, `AggregateRoot<TId>`, `ValueObject`, `IDomainEvent`, `IAuditable` (`DomainException` ya existe desde la feature 02) + VOs comunes (`FullName`, `Email`, `PhoneNumber`, `Address`) + unit tests. Además, los proyectos de tests pasan a `tests/` en la raíz | `specs/03-domain-abstractions-and-common-value-objects.md` |
| 04 | Persistencia base | `IApplicationDbContext`, `DispensarioDbContext`, interceptores de auditoría y domain events, conversión de IDs tipados, SQLite, `AddInfrastructure` | `specs/04-persistence-base.md` |
| 05 | Host de la API | `Program.cs`, `AddApplication` (MediatR + behaviors + validadores), Carter, OpenAPI + Scalar con Bearer, ProblemDetails, `/health`, appsettings y user-secrets | `specs/05-api-host.md` |

### Fase 1 — Identidad
| # | Feature | Contenido | Spec |
|---|---|---|---|
| 06 | Users: dominio y persistencia | Aggregate `User`, `Role`, configuración EF e índice único de email | `specs/06-users-domain-and-persistence.md` |
| 07 | Auth: login y JWT | `IPasswordHasher`, `IJwtTokenGenerator`, `ICurrentUser`, `POST /api/auth/login`, `GET /api/auth/me`, políticas, seed del Admin (con `EnsureCreated` en Development hasta la fila 17) | `specs/07-auth-login-and-jwt.md` |
| 08 | Gestión de usuarios | RegisterUser (autoregistro de recepcionistas y alta por el Admin), ChangePassword, ResetUserPassword, UpdateUserProfile, Deactivate/ActivateUser, GetUsers, GetUserById | `specs/08-user-management.md` |

### Fase 2 — Catálogos clínicos
| # | Feature | Contenido | Spec |
|---|---|---|---|
| 09 | Doctors: CRUD | Aggregate `Doctor` (matrícula única, especialidad, duración de la cita), Create/Update/Deactivate, GetDoctors/GetById, vínculo opcional con `User` | `specs/09-doctors-crud.md` |
| 10 | Doctors: horario semanal | `WeeklySchedule` sin solapes, SetWeeklySchedule, `IsAvailableAt` | `specs/10-doctor-weekly-schedule.md` |
| 11 | Patients: CRUD | Aggregate `Patient` (documento único), Create/Update/Deactivate, GetPatients (búsqueda + paginación), GetById, GetByDocument | `specs/11-patients-crud.md` |

### Fase 3 — Agenda y atención
| # | Feature | Contenido | Spec |
|---|---|---|---|
| 12 | Appointments: agendar | Aggregate `Appointment`, ScheduleAppointment (horario, duración, sin cruces de doctor ni paciente), GetAppointments (filtros) y GetById | `specs/12-appointments-scheduling.md` |
| 13 | Appointments: ciclo de vida | Confirm, Reschedule, Cancel, MarkNoShow; máquina de estados | `specs/13-appointments-lifecycle.md` |
| 14 | Slots disponibles | `GET /api/doctors/{id}/available-slots?date=` | `specs/14-available-slots.md` |
| 15 | Consultations | Aggregate `Consultation`, Start/Update/Close, validación de que el doctor es el usuario actual, GetById, historia del paciente | `specs/15-consultations.md` |
| 16 | Consulta cerrada → cita completada | `ConsultationClosedDomainEvent` + handler en Appointments | — |

### Fase 4 — Cierre de v1
| # | Feature | Contenido | Spec |
|---|---|---|---|
| 17 | Migración inicial y seed | `InitialCreate`; `InitialiseDatabaseAsync` cambia `EnsureCreatedAsync` por `MigrateAsync` (borrar `dispensario.db` una vez); datos de ejemplo opcionales (el seed del Admin ya existe desde la fila 07) | — |
| 18 | Tests de integración E2E | Flujo completo login → doctor → paciente → cita → consulta → cita Completed, más casos 401/403/404/409 | — |

> Los tests unitarios de cada feature van **dentro de su propio spec**, no en una fase aparte.

## Futuro (no planificado aún)
Frontend (`/frontend`), signos vitales, recetas, exámenes, farmacia/inventario, recordatorios de citas, bloqueos de agenda (vacaciones o feriados), auditoría avanzada, migración a PostgreSQL y extracción de microservicios (ver ARCHITECTURE §3).

## Preguntas abiertas
- ¿Se pagan las consultas o se registra algún cobro? (por ahora no)
- ~~¿Zona horaria del dispensario?~~ Cerrada en el spec 12: se guarda en UTC, la Api usa horas locales sin offset y la zona se configura en `Clinic:TimeZone` (`America/Guayaquil` por defecto).
- ¿Feriados o bloqueos de agenda del doctor (vacaciones)?
- ~~¿Tipos de documento válidos?~~ Cerrada en el spec 06: `NationalId` y `Passport` (`ForeignerId` cuando haga falta).
