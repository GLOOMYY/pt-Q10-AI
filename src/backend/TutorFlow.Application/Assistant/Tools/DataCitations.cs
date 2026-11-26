using TutorFlow.Application.Ports.AcademicApi;

namespace TutorFlow.Application.Assistant.Tools;

/// <summary>Data citations, with the exact format of R-RES-02. The period always comes from <c>GET /salud</c>.</summary>
public static class DataCitations
{
    /// <summary><c>documento</c> of a student's own data.</summary>
    public const string StudentDocument = "Datos del estudiante";

    /// <summary><c>documento</c> of data a coordinator consulted about their program.</summary>
    public const string ProgramDocument = "Datos del programa";

    /// <summary><c>{ "Datos del estudiante", "Notas · periodo &lt;periodo&gt;" }</c>.</summary>
    /// <param name="period">Period from <c>GET /salud</c>.</param>
    /// <returns>The citation.</returns>
    public static Citation Grades(string period) => new(StudentDocument, $"Notas · periodo {period}");

    /// <summary><c>{ "Datos del estudiante", "Asistencia · periodo &lt;periodo&gt;" }</c>.</summary>
    /// <param name="period">Period from <c>GET /salud</c>.</param>
    /// <returns>The citation.</returns>
    public static Citation Attendance(string period) => new(StudentDocument, $"Asistencia · periodo {period}");

    /// <summary><c>{ "Datos del programa", "&lt;programaId&gt; · periodo &lt;periodo&gt;" }</c>, e.g. <c>ADM · periodo 2026-2</c>.</summary>
    /// <param name="programId">The program.</param>
    /// <param name="period">Period from <c>GET /salud</c>.</param>
    /// <returns>The citation.</returns>
    public static Citation Program(ProgramId programId, string period)
    {
        ArgumentNullException.ThrowIfNull(programId);
        return new(ProgramDocument, $"{programId.Value} · periodo {period}");
    }

    /// <summary>
    /// The data citations this request may return: only for data the tools really used (R-RES-02, R-FUE-02).
    /// A student cites "Datos del estudiante"; a coordinator, "Datos del programa" of each program used.
    /// </summary>
    /// <param name="state">What the tools used.</param>
    /// <returns>The allowed data citations, in a stable order.</returns>
    public static IReadOnlyList<Citation> Allowed(ToolRunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Period is not { } period)
        {
            return [];
        }

        var citations = new List<Citation>();
        if (state.UsedOwnGrades)
        {
            citations.Add(Grades(period));
        }

        if (state.UsedOwnAttendance)
        {
            citations.Add(Attendance(period));
        }

        citations.AddRange(state.ProgramsUsed.Select(p => Program(p, period)));
        return citations;
    }
}
