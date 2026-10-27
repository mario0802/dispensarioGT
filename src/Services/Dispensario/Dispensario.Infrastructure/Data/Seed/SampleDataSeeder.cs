using Dispensario.Application.Abstractions;
using Dispensario.Domain.Common;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dispensario.Infrastructure.Data.Seed;

/// <summary>
/// Siembra en Development doctores con horario, pacientes y dos usuarios de ejemplo si la base no tiene doctores ni pacientes.
/// </summary>
// Los datos son válidos por construcción: un DomainException aquí es un error de programación
public sealed class SampleDataSeeder(
    DispensarioDbContext dbContext,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider,
    IOptions<SampleDataSeedOptions> options,
    ILogger<SampleDataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var sampleData = options.Value;

        if (!sampleData.Enabled)
            return;

        // Solo sobre una base sin doctores ni pacientes: no se mezclan datos reales con los de ejemplo
        if (await dbContext.Doctors.AnyAsync(cancellationToken) || await dbContext.Patients.AnyAsync(cancellationToken))
            return;

        var doctors = CreateDoctors();
        var patients = CreatePatients(DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));
        var users = new List<User>();

        if (string.IsNullOrWhiteSpace(sampleData.Password))
            logger.LogWarning("Sample users skipped: 'Seed:SampleData:Password' is empty.");
        else
            users.AddRange(await CreateUsersAsync(sampleData.Password, doctors.General.Id, cancellationToken));

        dbContext.Doctors.AddRange(doctors.All);
        dbContext.Patients.AddRange(patients);
        dbContext.Users.AddRange(users);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Sample data seeded: {Doctors} doctors, {Patients} patients, {Users} users.",
            doctors.All.Count,
            patients.Count,
            users.Count);
    }

    private static SampleDoctors CreateDoctors()
    {
        var weekdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };

        var general = CreateDoctor("Ana", "Torres", "MG-0001", "Medicina General", "doctor@dispensario.local", 30);
        general.SetWeeklySchedule(weekdays.SelectMany(day => new[]
        {
            Block(day, 8, 12),
            Block(day, 14, 18)
        }));

        var pediatrics = CreateDoctor("Pedro", "Salazar", "PED-0002", "Pediatría", "pediatria@dispensario.local", 20);
        pediatrics.SetWeeklySchedule(weekdays.Select(day => Block(day, 8, 13)));

        var cardiology = CreateDoctor("Lucía", "Benítez", "CAR-0003", "Cardiología", "cardiologia@dispensario.local", 40);
        cardiology.SetWeeklySchedule([Block(DayOfWeek.Tuesday, 9, 13), Block(DayOfWeek.Thursday, 9, 13)]);

        return new SampleDoctors(general, [general, pediatrics, cardiology]);
    }

    private static Doctor CreateDoctor(
        string firstName,
        string lastName,
        string licenseNumber,
        string specialty,
        string email,
        int appointmentDurationMinutes) =>
        Doctor.Create(
            FullName.Create(firstName, lastName),
            licenseNumber,
            specialty,
            Email.Create(email),
            phone: null,
            appointmentDurationMinutes);

    // Horas en punto, en hora local del dispensario
    private static ScheduleBlock Block(DayOfWeek day, int startHour, int endHour) =>
        ScheduleBlock.Create(day, new TimeOnly(startHour, 0), new TimeOnly(endHour, 0));

    private static List<Patient> CreatePatients(DateOnly today) =>
    [
        Patient.Create(
            NationalId("1710000001"),
            FullName.Create("Carlos", "Mendoza"),
            new DateOnly(1985, 3, 14),
            Gender.Male,
            email: null,
            PhoneNumber.Create("0991234567"),
            Address.Create("Av. Amazonas N24-03", "Quito", "Pichincha"),
            BloodType.OPositive,
            allergies: null,
            today),
        Patient.Create(
            NationalId("1710000002"),
            FullName.Create("María", "Vera"),
            new DateOnly(1992, 7, 8),
            Gender.Female,
            Email.Create("maria.vera@example.com"),
            phone: null,
            address: null,
            BloodType.APositive,
            "Penicilina",
            today),
        Patient.Create(
            NationalId("0910000003"),
            FullName.Create("Luis", "Andrade"),
            new DateOnly(2018, 11, 20),
            Gender.Male,
            email: null,
            phone: null,
            address: null,
            bloodType: null,
            allergies: null,
            today),
        Patient.Create(
            NationalId("0910000004"),
            FullName.Create("Sofía", "Cedeño"),
            new DateOnly(1960, 1, 30),
            Gender.Female,
            email: null,
            phone: null,
            address: null,
            BloodType.BNegative,
            allergies: null,
            today),
        Patient.Create(
            NationalId("1310000005"),
            FullName.Create("Jorge", "Paredes"),
            new DateOnly(2001, 5, 5),
            Gender.Male,
            email: null,
            phone: null,
            address: null,
            BloodType.ABPositive,
            allergies: null,
            today)
    ];

    // Omite el usuario cuyo email ya existe: el índice único haría fallar el arranque
    private async Task<List<User>> CreateUsersAsync(string password, DoctorId generalDoctorId, CancellationToken cancellationToken)
    {
        // Un solo hash para los dos usuarios: bcrypt con factor 12 tarda unos 250 ms
        var passwordHash = passwordHasher.Hash(password);
        var users = new List<User>();

        var receptionistEmail = Email.Create("recepcion@dispensario.local");
        if (!await EmailExistsAsync(receptionistEmail, cancellationToken))
        {
            users.Add(User.Create(
                receptionistEmail,
                passwordHash,
                FullName.Create("Rosa", "Molina"),
                NationalId("1700000010"),
                phone: null,
                Role.Receptionist));
        }

        var doctorEmail = Email.Create("doctor@dispensario.local");
        if (!await EmailExistsAsync(doctorEmail, cancellationToken))
        {
            var doctorUser = User.Create(
                doctorEmail,
                passwordHash,
                FullName.Create("Ana", "Torres"),
                NationalId("1700000011"),
                phone: null,
                Role.Doctor);
            doctorUser.LinkDoctor(generalDoctorId);
            users.Add(doctorUser);
        }

        return users;
    }

    private Task<bool> EmailExistsAsync(Email email, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken);

    private static Document NationalId(string number) => Document.Create(DocumentType.NationalId, number);

    // General es el doctor al que se vincula el usuario Doctor de ejemplo
    private sealed record SampleDoctors(Doctor General, IReadOnlyCollection<Doctor> All);
}
