using TutorFlow.Application.Ports;
using TutorFlow.Application.Ports.AcademicApi;
using TutorFlow.Testing.AcademicApi;
using Xunit;

namespace TutorFlow.Infrastructure.Tests.AcademicApi;

public sealed class FakeAcademicApiTests
{
    private static StudentId Student(string raw) => StudentId.TryParse(raw, out var id) ? id : throw new InvalidOperationException(raw);

    [Fact]
    public async Task Serves_period_programs_and_coordinators_from_datos_json()
    {
        var api = FakeAcademicApi.FromDefaultData();

        Assert.Equal("2026-2", (await api.GetHealthAsync(default)).Period);
        Assert.Equal(2, (await api.GetProgramsAsync(default)).Count);
        Assert.True(CoordinatorId.TryParse("COORD-01", out var coord));
        Assert.Equal(["ING-SIS"], (await api.GetCoordinatorAsync(coord, default))!.ProgramIds);
    }

    [Fact]
    public async Task Unknown_ids_return_null_and_known_students_return_lists()
    {
        var api = FakeAcademicApi.FromDefaultData();

        Assert.Null(await api.GetStudentAsync(Student("EST-999"), default));
        Assert.Null(await api.GetGradesAsync(Student("EST-999"), default));
        Assert.NotEmpty((await api.GetGradesAsync(Student("EST-001"), default))!);
        Assert.NotEmpty((await api.GetAttendanceAsync(Student("EST-001"), default))!);
        Assert.True(ProgramId.TryParse("ADM", out var program));
        Assert.All((await api.GetProgramStudentsAsync(program, default))!, s => Assert.Equal("ADM", s.ProgramId));
    }

    [Fact]
    public async Task Records_exact_routes_and_can_be_cleared()
    {
        var api = FakeAcademicApi.FromDefaultData();

        await api.GetStudentAsync(Student("EST-001"), default);
        await api.GetGradesAsync(Student("EST-001"), default);

        Assert.Equal(["/estudiantes/EST-001", "/estudiantes/EST-001/notas"], api.Calls);
        api.ClearCalls();
        Assert.Empty(api.Calls);
    }

    [Fact]
    public async Task Stopped_api_throws_dependency_unavailable_and_still_records_the_attempt()
    {
        var api = FakeAcademicApi.CreateStopped();

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(() => api.GetStudentAsync(Student("EST-001"), default));

        Assert.Equal(ExternalDependency.AcademicApi, ex.Dependency);
        Assert.Equal(["/estudiantes/EST-001"], api.Calls);
        api.Start();
        Assert.NotNull(await api.GetStudentAsync(Student("EST-001"), default));
    }
}
