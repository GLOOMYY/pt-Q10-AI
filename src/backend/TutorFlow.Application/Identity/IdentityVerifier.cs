using TutorFlow.Application.Academic;
using TutorFlow.Application.Assistant;
using TutorFlow.Application.Ports.AcademicApi;

namespace TutorFlow.Application.Identity;

/// <summary>
/// Checks that a well-formed identity exists in the simulated API and matches its role (R-SEG-04):
/// <list type="bullet">
/// <item><description>Coordinator: <c>GET /coordinadores/{id}</c>, which also gives the programs in charge (API-08).</description></item>
/// <item><description>Student: <c>GET /programas</c> + <c>GET /programas/{id}/estudiantes</c>; never
/// <c>/estudiantes/{id}</c>.</description></item>
/// </list>
/// These calls never count in <c>estudiantesConsultados</c> nor in <c>usoDatosEstudiante</c> (R-SEG-04, R-LOG-06,
/// R-RES-04). When the API does not answer, <see cref="Ports.DependencyUnavailableException"/> propagates (503).
/// </summary>
public sealed class IdentityVerifier
{
    private readonly IAcademicDirectory _directory;

    /// <summary>Creates the verifier.</summary>
    /// <param name="directory">The per-request academic directory.</param>
    public IdentityVerifier(IAcademicDirectory directory) => _directory = directory;

    /// <summary>Verifies the identity.</summary>
    /// <param name="identity">Identity with a valid format (<see cref="UserIdentity.TryCreate"/>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The session user, or <see langword="null"/> if the id does not exist for that role (403, without
    /// telling why, D-08).</returns>
    public async Task<SessionUser?> VerifyAsync(UserIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return identity.Role switch
        {
            UserRole.Coordinator => await VerifyCoordinatorAsync(identity, cancellationToken).ConfigureAwait(false),
            UserRole.Student => await VerifyStudentAsync(identity, cancellationToken).ConfigureAwait(false),
            _ => null,
        };
    }

    private async Task<SessionUser?> VerifyCoordinatorAsync(UserIdentity identity, CancellationToken cancellationToken)
    {
        var coordinator = await _directory.GetCoordinatorAsync(identity.CoordinatorId!, cancellationToken).ConfigureAwait(false);
        if (coordinator is null || !string.Equals(coordinator.Id, identity.Id, StringComparison.Ordinal))
        {
            return null;
        }

        // API-01: program ids returned by the API are validated before they are ever put in a route.
        var programs = new List<ProgramId>();
        foreach (var raw in coordinator.ProgramIds)
        {
            if (ProgramId.TryParse(raw, out var programId) && !programs.Contains(programId))
            {
                programs.Add(programId);
            }
        }

        return new SessionUser(identity, programs, studentRecord: null);
    }

    private async Task<SessionUser?> VerifyStudentAsync(UserIdentity identity, CancellationToken cancellationToken)
    {
        var rosters = await _directory.GetAllRostersAsync(cancellationToken).ConfigureAwait(false);
        StudentRecord? record = null;
        var programs = new List<ProgramId>();
        foreach (var roster in rosters)
        {
            var match = roster.Students.FirstOrDefault(s => string.Equals(s.Id, identity.Id, StringComparison.Ordinal));
            if (match is not null)
            {
                record ??= match;
                programs.Add(roster.ProgramId);
            }
        }

        return record is null ? null : new SessionUser(identity, programs, record);
    }
}
