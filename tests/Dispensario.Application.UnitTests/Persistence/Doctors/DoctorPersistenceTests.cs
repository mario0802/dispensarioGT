using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.UnitTests.Persistence.Doctors;

public sealed class DoctorPersistenceTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task SaveChangesAsync_WithAllFields_ReadsBackEqualDoctor()
    {
        var doctor = TestDoctors.Create(licenseNumber: "mp-123", phone: "0991234567", appointmentDurationMinutes: 45);
        await AddAsync(doctor);

        await using var context = _db.CreateContext();
        var loaded = await context.Doctors.SingleAsync(TestContext.Current.CancellationToken);

        loaded.Id.ShouldBe(doctor.Id);
        loaded.FullName.ShouldBe(doctor.FullName);
        loaded.LicenseNumber.ShouldBe("MP-123");
        loaded.Specialty.ShouldBe(doctor.Specialty);
        loaded.Email.ShouldBe(doctor.Email);
        loaded.Phone.ShouldBe(doctor.Phone);
        loaded.AppointmentDurationMinutes.ShouldBe(45);
        loaded.IsActive.ShouldBeTrue();
        loaded.CreatedAtUtc.ShouldBe(DispensarioTestDb.StartTime.UtcDateTime);
        loaded.CreatedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task SaveChangesAsync_WithoutPhone_ReadsBackNullPhone()
    {
        await AddAsync(TestDoctors.Create(phone: null));

        await using var context = _db.CreateContext();
        var loaded = await context.Doctors.SingleAsync(TestContext.Current.CancellationToken);

        loaded.Phone.ShouldBeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_WithDuplicateLicenseNumber_ThrowsDbUpdateException()
    {
        await AddAsync(TestDoctors.Create(licenseNumber: "MP-123"));

        var act = () => AddAsync(TestDoctors.Create(licenseNumber: "mp-123"));

        await act.ShouldThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SaveChangesAsync_WithTwoUsersLinkedToSameDoctor_ThrowsDbUpdateException()
    {
        var doctorId = DoctorId.New();
        await AddAsync(LinkedUser("ana@dispensario.local", "111", doctorId));

        var act = () => AddAsync(LinkedUser("luis@dispensario.local", "222", doctorId));

        await act.ShouldThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SaveChangesAsync_WithSeveralUnlinkedUsers_SavesAll()
    {
        await AddAsync(TestUsers.Create(Role.Doctor, "ana@dispensario.local", "111"));
        await AddAsync(TestUsers.Create(Role.Doctor, "luis@dispensario.local", "222"));

        await using var context = _db.CreateContext();
        var count = await context.Users.CountAsync(u => u.DoctorId == null, TestContext.Current.CancellationToken);

        count.ShouldBe(2);
    }

    [Fact]
    public async Task EnsureCreated_DoctorsTable_HasExpectedColumns()
    {
        var columns = new List<string>();
        await using var command = _db.Connection.CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_info('Doctors')";
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            columns.Add(reader.GetString(0));

        columns.ShouldBe(
            [
                "Id", "FullName_FirstName", "FullName_LastName", "LicenseNumber", "Specialty", "Email", "Phone",
                "AppointmentDurationMinutes", "IsActive", "CreatedAtUtc", "CreatedBy", "LastModifiedAtUtc", "LastModifiedBy"
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task SaveChangesAsync_WithSchedule_ReadsBackEqualBlocks()
    {
        var doctor = TestDoctors.Create();
        var monday = Block(DayOfWeek.Monday, 9, 0, 12, 30);
        var sunday = Block(DayOfWeek.Sunday, 14, 5, 18, 55);
        doctor.SetWeeklySchedule([monday, sunday]);
        await AddAsync(doctor);

        await using var context = _db.CreateContext();
        var loaded = await context.Doctors.SingleAsync(TestContext.Current.CancellationToken);

        loaded.Schedule.ShouldBe([monday, sunday], ignoreOrder: true);
    }

    [Fact]
    public async Task SaveChangesAsync_WithSchedule_StoresDayOfWeekAsText()
    {
        var doctor = TestDoctors.Create();
        doctor.SetWeeklySchedule([Block(DayOfWeek.Wednesday, 9, 0, 12, 0)]);
        await AddAsync(doctor);

        await using var command = _db.Connection.CreateCommand();
        command.CommandText = "SELECT DayOfWeek FROM DoctorScheduleBlocks";
        var day = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        day.ShouldBe("Wednesday");
    }

    [Fact]
    public async Task SaveChangesAsync_ReplacingSchedule_DeletesPreviousRows()
    {
        var doctor = TestDoctors.Create();
        doctor.SetWeeklySchedule([Block(DayOfWeek.Monday, 9, 0, 12, 0), Block(DayOfWeek.Tuesday, 9, 0, 12, 0)]);
        await AddAsync(doctor);
        var friday = Block(DayOfWeek.Friday, 8, 0, 10, 0);
        // El mismo bloque del lunes vuelve a entrar: con igualdad por valor no debe confundir al change tracker
        var monday = Block(DayOfWeek.Monday, 9, 0, 12, 0);

        await UpdateAsync(doctor.Id, d => d.SetWeeklySchedule([monday, friday]));

        (await LoadAsync(doctor.Id)).Schedule.ShouldBe([monday, friday], ignoreOrder: true);
        (await CountScheduleRowsAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task SaveChangesAsync_ClearingSchedule_DeletesAllRows()
    {
        var doctor = TestDoctors.Create();
        doctor.SetWeeklySchedule([Block(DayOfWeek.Monday, 9, 0, 12, 0)]);
        await AddAsync(doctor);

        await UpdateAsync(doctor.Id, d => d.SetWeeklySchedule([]));

        (await LoadAsync(doctor.Id)).Schedule.ShouldBeEmpty();
        (await CountScheduleRowsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SaveChangesAsync_ChangingOnlySchedule_UpdatesLastModifiedAtUtc()
    {
        var doctor = TestDoctors.Create();
        await AddAsync(doctor);
        _db.TimeProvider.Advance(TimeSpan.FromMinutes(5));

        await UpdateAsync(doctor.Id, d => d.SetWeeklySchedule([Block(DayOfWeek.Monday, 9, 0, 12, 0)]));

        (await LoadAsync(doctor.Id)).LastModifiedAtUtc.ShouldBe(DispensarioTestDb.StartTime.AddMinutes(5).UtcDateTime);
    }

    [Fact]
    public async Task EnsureCreated_DoctorScheduleBlocksTable_HasExpectedColumns()
    {
        var columns = new List<string>();
        await using var command = _db.Connection.CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_info('DoctorScheduleBlocks')";
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            columns.Add(reader.GetString(0));

        columns.ShouldBe(["DoctorId", "Id", "DayOfWeek", "StartTime", "EndTime"], ignoreOrder: true);
    }

    public void Dispose() => _db.Dispose();

    private static ScheduleBlock Block(DayOfWeek day, int startHour, int startMinute, int endHour, int endMinute) =>
        ScheduleBlock.Create(day, new TimeOnly(startHour, startMinute), new TimeOnly(endHour, endMinute));

    private async Task UpdateAsync(DoctorId doctorId, Action<Doctor> change)
    {
        await using var context = _db.CreateContext();
        var doctor = await context.Doctors.SingleAsync(d => d.Id == doctorId, TestContext.Current.CancellationToken);
        change(doctor);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Doctor> LoadAsync(DoctorId doctorId)
    {
        await using var context = _db.CreateContext();
        return await context.Doctors.SingleAsync(d => d.Id == doctorId, TestContext.Current.CancellationToken);
    }

    private async Task<long> CountScheduleRowsAsync()
    {
        await using var command = _db.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM DoctorScheduleBlocks";
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static User LinkedUser(string email, string documentNumber, DoctorId doctorId)
    {
        var user = TestUsers.Create(Role.Doctor, email, documentNumber);
        user.LinkDoctor(doctorId);
        return user;
    }

    private async Task AddAsync<TEntity>(TEntity entity) where TEntity : class
    {
        await using var context = _db.CreateContext();
        context.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
