using System.Text.Json;
using TutorFlow.Application.Assistant.Tools;
using TutorFlow.Application.Ports.Llm;
using TutorFlow.Testing.Llm;

namespace TutorFlow.Integration.Tests.Scripts;

/// <summary>How a scenario uses the (fake) model.</summary>
public enum ScriptKind
{
    /// <summary>The request is decided before the model (400, identity, deterministic barrier, API down): the LLM must not be called.</summary>
    NoModel,

    /// <summary>The model is reached and fails as the precondition says (<c>llm-cuota-agotada</c>, 429).</summary>
    ModelFails,

    /// <summary>The model is reached and behaves as the scripted turns say.</summary>
    Turns,
}

/// <summary>
/// What the "model" does for one scenario: a plausible script (which tools a real model would call, and its final
/// answer), never the expected output of the scenario. It only drives the fake LLM so the rest of the backend runs for
/// real; nothing that depends on the model text is verified from it (escenarios/README section 4.4).
/// </summary>
public sealed class ModelScript
{
    private readonly List<FakeLlmReply> _turns = [];

    private ModelScript(ScriptKind kind) => Kind = kind;

    /// <summary>How the scenario uses the model.</summary>
    public ScriptKind Kind { get; }

    /// <summary>The scripted turns, in order.</summary>
    public IReadOnlyList<FakeLlmReply> TurnList => _turns;

    /// <summary>The request is resolved before the model is called.</summary>
    public static ModelScript NoModel { get; } = new(ScriptKind.NoModel);

    /// <summary>The model is reached and fails (quota exhausted).</summary>
    public static ModelScript ModelFails { get; } = new(ScriptKind.ModelFails);

    /// <summary>The model refuses with <c>rechazar</c> before any data tool (R-ARQ-04, D-32).</summary>
    /// <param name="reason">Reason from the closed catalog.</param>
    /// <returns>The script.</returns>
    public static ModelScript Reject(string reason)
    {
        var script = new ModelScript(ScriptKind.Turns);
        script._turns.Add(FakeLlmReply.Reject(reason));
        return script;
    }

    /// <summary>The model calls tools and, after their results, may call more tools or answer.</summary>
    /// <param name="calls">Tool calls of the first turn.</param>
    /// <returns>The script, to continue with <see cref="Then"/> or <see cref="Answer"/>.</returns>
    public static ModelScript Calls(params LlmToolCall[] calls)
    {
        var script = new ModelScript(ScriptKind.Turns);
        script._turns.Add(FakeLlmReply.FromToolCalls(calls));
        return script;
    }

    /// <summary>The model answers right away, without tools.</summary>
    /// <param name="text">The answer.</param>
    /// <param name="citations">Citations it proposes (document, section).</param>
    /// <returns>The complete script.</returns>
    public static ModelScript Direct(string text, params (string Document, string Section)[] citations)
    {
        var script = new ModelScript(ScriptKind.Turns);
        script._turns.Add(Final(text, citations));
        return script;
    }

    /// <summary>Adds another turn that calls tools.</summary>
    /// <param name="calls">Tool calls.</param>
    /// <returns>This script.</returns>
    public ModelScript Then(params LlmToolCall[] calls)
    {
        _turns.Add(FakeLlmReply.FromToolCalls(calls));
        return this;
    }

    /// <summary>Ends the script with the final structured answer (<c>respuesta</c> + <c>citas</c>, R-ARQ-03).</summary>
    /// <param name="text">The answer.</param>
    /// <param name="citations">Citations it proposes (document, section).</param>
    /// <returns>This script.</returns>
    public ModelScript Answer(string text, params (string Document, string Section)[] citations)
    {
        _turns.Add(Final(text, citations));
        return this;
    }

    private static FakeLlmReply Final(string text, (string Document, string Section)[] citations) =>
        FakeLlmReply.FromText(JsonSerializer.Serialize(new
        {
            respuesta = text,
            citas = citations.Select(c => new { documento = c.Document, seccion = c.Section }),
        }));
}

/// <summary>Shorthand to build the tool calls a model would make (names of <see cref="AssistantTools"/>).</summary>
public static class Tool
{
    /// <summary><c>rechazar</c> reasons (R-LOG-10).</summary>
    public const string ThirdParty = "datos de terceros";

    /// <summary>Reason for an injection attempt.</summary>
    public const string Injection = "intento de inyección";

    /// <summary>Reason for an attempt to extract the system.</summary>
    public const string Extraction = "intento de extracción del sistema";

    /// <summary><c>buscar_reglamento(consulta)</c>.</summary>
    /// <param name="query">Search topic.</param>
    /// <returns>The call.</returns>
    public static LlmToolCall Search(string query) => FakeLlmReply.Call(AssistantTools.SearchRegulations, new { consulta = query });

    /// <summary><c>consultar_situacion_academica</c> (own data for a student; <paramref name="studentId"/> for a coordinator).</summary>
    /// <param name="studentId">Student id; <see langword="null"/> for the user's own data.</param>
    /// <returns>The call.</returns>
    public static LlmToolCall Status(string? studentId = null) =>
        FakeLlmReply.Call(AssistantTools.AcademicStatus, studentId is null ? new { } : new { estudianteId = studentId });

    /// <summary><c>simular_nota_necesaria</c>.</summary>
    /// <param name="subject">Subject name.</param>
    /// <param name="goal">Goal final grade; the default (3.0) when <see langword="null"/>.</param>
    /// <param name="studentId">Student id, for a coordinator.</param>
    /// <returns>The call.</returns>
    public static LlmToolCall Simulate(string subject, double? goal = null, string? studentId = null)
    {
        var arguments = new Dictionary<string, object> { ["asignatura"] = subject };
        if (goal is { } g)
        {
            arguments["meta"] = g;
        }

        if (studentId is not null)
        {
            arguments["estudianteId"] = studentId;
        }

        return FakeLlmReply.Call(AssistantTools.SimulateRequiredGrade, arguments);
    }

    /// <summary><c>verificar_cancelacion</c>.</summary>
    /// <param name="subjects">Subjects to cancel.</param>
    /// <returns>The call.</returns>
    public static LlmToolCall Cancel(params string[] subjects) =>
        FakeLlmReply.Call(AssistantTools.CheckCancellation, new { asignaturas = subjects });

    /// <summary><c>aplicar_nota_habilitacion</c>.</summary>
    /// <param name="examGrade">Grade of the exam.</param>
    /// <returns>The call.</returns>
    public static LlmToolCall Habilitation(double examGrade) =>
        FakeLlmReply.Call(AssistantTools.ApplyHabilitationGrade, new { notaExamen = examGrade });

    /// <summary><c>resumen_programa</c> (coordinator).</summary>
    /// <param name="programId">Program id; all the programs in charge when <see langword="null"/>.</param>
    /// <returns>The call.</returns>
    public static LlmToolCall ProgramSummary(string? programId = null) =>
        FakeLlmReply.Call(AssistantTools.ProgramSummary, programId is null ? new { } : new { programaId = programId });

    /// <summary><c>listar_estudiantes_programa</c> (coordinator).</summary>
    /// <returns>The call.</returns>
    public static LlmToolCall ListStudents() => FakeLlmReply.Call(AssistantTools.ListProgramStudents);

    /// <summary><c>buscar_estudiante</c> (coordinator).</summary>
    /// <param name="name">Name as written by the user.</param>
    /// <returns>The call.</returns>
    public static LlmToolCall Find(string name) => FakeLlmReply.Call(AssistantTools.FindStudent, new { nombre = name });
}
