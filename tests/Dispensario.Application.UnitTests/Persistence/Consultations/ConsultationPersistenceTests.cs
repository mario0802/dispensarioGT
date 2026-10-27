using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Consultations;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.UnitTests.Persistence.Consultations;

public sealed class ConsultationPersistenceTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task SaveChangesAsync_WithClosedConsultationWithAppointment_ReadsBackEqualConsultation()
    {
        var appointmentId = AppointmentId.New();
        var consultation = TestConsultations.Start(appointmentId: appointmentId);
        consultation.Update("Dolor de cabeza", null, "Notas previas");
        var closedAtUtc = TestConsultations.StartedAtUtc.AddMinutes(20);
        consultation.Close("Cefalea tensional", "Control en dos semanas.", closedAtUtc);
        await AddAsync(consultation);

        var loaded = await LoadAsync(consultation.Id);

        loaded.PatientId.ShouldBe(consultation.PatientId);
        loaded.DoctorId.ShouldBe(consultation.DoctorId);
        loaded.AppointmentId.ShouldBe(appointmentId);
        loaded.Reason.ShouldBe("Dolor de cabeza");
        loaded.Diagnosis.ShouldBe("Cefalea tensional");
        loaded.Notes.ShouldBe("Control en dos semanas.");
        loaded.Status.ShouldBe(ConsultationStatus.Closed);
        loaded.StartedAtUtc.ShouldBe(TestConsultations.StartedAtUtc);
        loaded.StartedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        loaded.ClosedAtUtc.ShouldBe(closedAtUtc);
        loaded.ClosedAtUtc!.Value.Kind.ShouldBe(DateTimeKind.Utc);
        loaded.CreatedAtUtc.ShouldBe(DispensarioTestDb.StartTime.UtcDateTime);
    }

    [Fact]
    public async Task SaveChangesAsync_WithOpenConsultationWithoutAppointment_ReadsBackNulls()
    {
        var consultation = TestConsultations.Start();
        await AddAsync(consultation);

        var loaded = await LoadAsync(consultation.Id);

        loaded.AppointmentId.ShouldBeNull();
        loaded.Diagnosis.ShouldBeNull();
        loaded.Notes.ShouldBeNull();
        loaded.ClosedAtUtc.ShouldBeNull();
        loaded.Status.ShouldBe(ConsultationStatus.Open);
    }

    [Fact]
    public async Task SaveChangesAsync_WithConsultation_StoresStatusAsText()
    {
        await AddAsync(TestConsultations.Start());

        var statuses = await QueryNamesAsync("SELECT Status FROM Consultations");

        statuses.ShouldBe(["Open"]);
    }

    [Fact]
    public async Task SaveChangesAsync_WithTwoConsultationsWithoutAppointment_SavesBoth()
    {
        await AddAsync(TestConsultations.Start());
        await AddAsync(TestConsultations.Start());

        await using var context = _db.CreateContext();
        var count = await context.Consultations.CountAsync(TestContext.Current.CancellationToken);

        count.ShouldBe(2);
    }

    [Fact]
    public async Task SaveChangesAsync_WithTwoConsultationsForSameAppointment_ThrowsDbUpdateException()
    {
        var appointmentId = AppointmentId.New();
        await AddAsync(TestConsultations.Start(appointmentId: appointmentId));

        await Should.ThrowAsync<DbUpdateException>(() => AddAsync(TestConsultations.Start(appointmentId: appointmentId)));
    }

    [Fact]
    public async Task Migrate_ConsultationsTable_HasExpectedColumns()
    {
        var columns = await QueryNamesAsync("SELECT name FROM pragma_table_info('Consultations')");

        columns.ShouldBe(
            [
                "Id", "PatientId", "DoctorId", "AppointmentId", "Reason", "Diagnosis", "Notes", "Status",
                "StartedAtUtc", "ClosedAtUtc", "CreatedAtUtc", "CreatedBy", "LastModifiedAtUtc", "LastModifiedBy"
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Migrate_ConsultationsTable_HasNullableOptionalColumns()
    {
        var nullable = await QueryNamesAsync(
            "SELECT name FROM pragma_table_info('Consultations') WHERE \"notnull\" = 0 AND pk = 0");

        nullable.ShouldContain("AppointmentId");
        nullable.ShouldContain("Diagnosis");
        nullable.ShouldContain("Notes");
        nullable.ShouldContain("ClosedAtUtc");
        nullable.ShouldNotContain("Reason");
        nullable.ShouldNotContain("PatientId");
    }

    [Fact]
    public async Task Migrate_ConsultationsTable_HasIndexesAndNoForeignKeys()
    {
        var uniqueIndexColumns = await QueryNamesAsync(
            "SELECT group_concat(c.name, ',') FROM pragma_index_list('Consultations') i " +
            "JOIN pragma_index_info(i.name) c WHERE i.\"unique\" = 1 AND i.origin = 'c' GROUP BY i.name");
        var indexColumns = await QueryNamesAsync(
            "SELECT group_concat(c.name, ',') FROM pragma_index_list('Consultations') i " +
            "JOIN pragma_index_info(i.name) c GROUP BY i.name");
        var foreignKeys = await QueryNamesAsync("SELECT \"table\" FROM pragma_foreign_key_list('Consultations')");

        uniqueIndexColumns.ShouldBe(["AppointmentId"]);
        indexColumns.ShouldContain("PatientId,StartedAtUtc");
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

    private async Task AddAsync(Consultation consultation)
    {
        await using var context = _db.CreateContext();
        context.Consultations.Add(consultation);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Consultation> LoadAsync(ConsultationId id)
    {
        await using var context = _db.CreateContext();
        return await context.Consultations.SingleAsync(c => c.Id == id, TestContext.Current.CancellationToken);
    }
}
