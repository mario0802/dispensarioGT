using BuildingBlocks.Exceptions;
using Dispensario.Application.Doctors.Dtos;
using Dispensario.Application.Doctors.Queries.GetDoctorById;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users;

namespace Dispensario.Application.UnitTests.Doctors;

public sealed class GetDoctorByIdHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_WithLinkedDoctor_ReturnsDtoWithUserIdAndCreatedAt()
    {
        var doctor = TestDoctors.Create(phone: "0991234567", appointmentDurationMinutes: 20);
        var user = TestUsers.Create(Role.Doctor);
        user.LinkDoctor(doctor.Id);
        await using (var context = _db.CreateContext())
        {
            context.AddRange(doctor, user);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var dto = await HandleAsync(doctor.Id.Value);

        dto.ShouldBe(new DoctorDto(
            doctor.Id.Value,
            "Luis",
            "Mora",
            "MP-123",
            "Pediatría",
            "luis.mora@dispensario.local",
            "0991234567",
            20,
            true,
            user.Id.Value,
            DispensarioTestDb.StartTime.UtcDateTime));
    }

    [Fact]
    public async Task Handle_WithUnlinkedDoctor_ReturnsNullUserId()
    {
        var doctor = TestDoctors.Create();
        await using (var context = _db.CreateContext())
        {
            context.Add(doctor);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var dto = await HandleAsync(doctor.Id.Value);

        dto.UserId.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_WithUnknownDoctor_ThrowsNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(DoctorId.New().Value));
    }

    public void Dispose() => _db.Dispose();

    private async Task<DoctorDto> HandleAsync(Guid id)
    {
        await using var context = _db.CreateContext();
        return await new GetDoctorByIdHandler(context).Handle(new GetDoctorByIdQuery(id), TestContext.Current.CancellationToken);
    }
}
