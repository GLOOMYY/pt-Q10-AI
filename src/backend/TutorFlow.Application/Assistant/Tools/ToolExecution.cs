using System.Text.Json;
using TutorFlow.Application.Ports.AcademicApi;

namespace TutorFlow.Application.Assistant.Tools;

/// <summary>Kind of result of a tool call.</summary>
public enum ToolExecutionKind
{
    /// <summary>The tool ran; its result goes back to the model.</summary>
    Completed = 0,

    /// <summary>The tool asked for data the user may not see: the backend answers 403 (R-SEG-06).</summary>
    Unauthorized = 1,

    /// <summary>The model called <c>rechazar(motivo)</c>: the backend answers 403 with that reason (R-ARQ-04).</summary>
    Rejected = 2,
}

/// <summary>Result of one tool call.</summary>
public sealed record ToolExecution
{
    private ToolExecution(ToolExecutionKind kind, JsonElement result, string? reason)
    {
        Kind = kind;
        Result = result;
        Reason = reason;
    }

    /// <summary>Kind of result.</summary>
    public ToolExecutionKind Kind { get; }

    /// <summary>JSON returned to the model when <see cref="Kind"/> is <see cref="ToolExecutionKind.Completed"/>.</summary>
    public JsonElement Result { get; }

    /// <summary>Audit reason of a 403 (R-LOG-10); <see langword="null"/> when completed.</summary>
    public string? Reason { get; }

    /// <summary>A completed call.</summary>
    /// <param name="result">The JSON for the model: only authorized, already computed data (R-ARQ-02).</param>
    /// <returns>The execution.</returns>
    public static ToolExecution Completed(JsonElement result) => new(ToolExecutionKind.Completed, result, null);

    /// <summary>A call for data the user may not see (R-SEG-06): "datos de terceros".</summary>
    /// <returns>The execution.</returns>
    public static ToolExecution Unauthorized() =>
        new(ToolExecutionKind.Unauthorized, default, Ports.Audit.AuditReasons.ThirdPartyData);

    /// <summary>The model rejected the request (R-ARQ-04).</summary>
    /// <param name="reason">A 403 reason of the catalog (R-LOG-10).</param>
    /// <returns>The execution.</returns>
    public static ToolExecution Rejected(string reason) => new(ToolExecutionKind.Rejected, default, reason);
}

/// <summary>A rule that the data of a request involve, and the word that shows an answer applies it.</summary>
/// <param name="Citation">The document citation of the rule.</param>
/// <param name="Trigger">Normalized word prefix (see <see cref="TextNormalizer.Normalize"/>), e.g. <c>inasistencia</c>.</param>
public sealed record RuleBacking(Citation Citation, string Trigger);

/// <summary>
/// What the tools of one request used, to compute <c>usoDatosEstudiante</c> (R-RES-04) and the data citations that
/// may be returned (R-RES-02). One instance per request: nothing survives between questions (R-RES-07).
/// </summary>
public sealed class ToolRunState
{
    private readonly List<ProgramId> _programs = [];
    private readonly List<RuleBacking> _rules = [];

    /// <summary>Whether academic data of a student was consulted to answer (R-RES-04).</summary>
    public bool UsedStudentData { get; private set; }

    /// <summary>Whether a student read their own grades (data citation "Notas · periodo").</summary>
    public bool UsedOwnGrades { get; private set; }

    /// <summary>Whether a student read their own attendance (data citation "Asistencia · periodo").</summary>
    public bool UsedOwnAttendance { get; private set; }

    /// <summary>Programs whose data a coordinator used (data citation "&lt;programa&gt; · periodo").</summary>
    public IReadOnlyList<ProgramId> ProgramsUsed => _programs;

    /// <summary>The period of <c>GET /salud</c>, once a data tool has fetched it.</summary>
    public string? Period { get; set; }

    /// <summary>
    /// Rules the data of this request really involve, each with the normalized word that shows the answer applies it
    /// (e.g. Artículo 11 when a consulted course was lost by absences). The citation validator adds a missing one when
    /// the answer uses it (R-RES-02: data + rule).
    /// </summary>
    public IReadOnlyList<RuleBacking> BackedRules => _rules;

    /// <summary>Records a rule that the data of this request involve.</summary>
    /// <param name="citation">The document citation of the rule; it must exist in the index.</param>
    /// <param name="trigger">Normalized word prefix whose presence in the answer shows the rule is applied.</param>
    public void BackRule(Citation citation, string trigger)
    {
        ArgumentNullException.ThrowIfNull(citation);
        ArgumentException.ThrowIfNullOrWhiteSpace(trigger);
        var backing = new RuleBacking(citation, trigger);
        if (!_rules.Contains(backing))
        {
            _rules.Add(backing);
        }
    }

    /// <summary>Records that a student used their own grades and attendance.</summary>
    public void MarkOwnData()
    {
        UsedStudentData = true;
        UsedOwnGrades = true;
        UsedOwnAttendance = true;
    }

    /// <summary>Records that a coordinator used data of a program (a roster or a student of it).</summary>
    /// <param name="programId">The program.</param>
    public void MarkProgramData(ProgramId programId)
    {
        ArgumentNullException.ThrowIfNull(programId);
        UsedStudentData = true;
        if (!_programs.Contains(programId))
        {
            _programs.Add(programId);
        }
    }
}
