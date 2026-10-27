using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Appointments;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.UnitTests.Persistence.Appointments;

public sealed class AppointmentPersistenceTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task SaveChangesAsync_WithAppointment_ReadsBackEqualAppointment()
    {
        var appointment = TestAppointments.Create(reason: "Control de presión");
        await AddAsync(appointment);

        await using var context = _db.CreateContext();
        var loaded = await context.Appointments.SingleAsync(a => a.Id == appointment.Id, TestContext.Current.CancellationToken);

        loaded.PatientId.ShouldBe(appointment.PatientId);
        loaded.DoctorId.ShouldBe(appointment.DoctorId);
        loaded.StartUtc.ShouldBe(appointment.StartUtc);
        loaded.StartUtc.Kind.ShouldBe(DateTimeKind.Utc);
        loaded.EndUtc.ShouldBe(appointment.EndUtc);
        loaded.EndUtc.Kind.ShouldBe(DateTimeKind.Utc);
        loaded.Reason.ShouldBe("Control de presión");
        loaded.Status.ShouldBe(AppointmentStatus.Scheduled);
        loaded.CreatedAtUtc.ShouldBe(DispensarioTestDb.StartTime.UtcDateTime);
        loaded.CreatedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task SaveChangesAsync_WithAppointment_StoresStatusAsText()
    {
        await AddAsync(TestAppointments.Create());

        await using var command = _db.Connection.CreateCommand();
        command.CommandText = "SELECT Status FROM Appointments";
        var status = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        status.ShouldBe("Scheduled");
    }

    [Fact]
    public async Task EnsureCreated_AppointmentsTable_HasExpectedColumns()
    {
        var columns = await QueryNamesAsync("SELECT name FROM pragma_table_info('Appointments')");

        columns.ShouldBe(
            [
                "Id", "PatientId", "DoctorId", "StartUtc", "EndUtc", "Reason", "Status",
                "CreatedAtUtc", "CreatedBy", "LastModifiedAtUtc", "LastModifiedBy"
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task EnsureCreated_AppointmentsTable_HasIndexesAndNoForeignKeys()
    {
        var indexColumns = await QueryNamesAsync(
            "SELECT group_concat(c.name, ',') FROM pragma_index_list('Appointments') i " +
            "JOIN pragma_index_info(i.name) c GROUP BY i.name");
        var foreignKeys = await QueryNamesAsync("SELECT \"table\" FROM pragma_foreign_key_list('Appointments')");

        indexColumns.ShouldContain("DoctorId,StartUtc");
        indexColumns.ShouldContain("PatientId,StartUtc");
        foreignKeys.ShouldBeEmpty();
    }

    public void Dispose() => _db.Dispose();

    private async Task<List<string>> QueryNamesAsync(string sql)
    {
        var names = new List<string>();
        await using var command = _db.Connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            names.Add(reader.GetString(0));
        return names;
    }

    private async Task AddAsync(Appointment appointment)
    {
        await using var context = _db.CreateContext();
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
