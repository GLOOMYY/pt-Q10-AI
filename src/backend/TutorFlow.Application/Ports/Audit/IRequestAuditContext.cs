using TutorFlow.Application.Assistant;
using TutorFlow.Application.Ports.AcademicApi;

namespace TutorFlow.Application.Ports.Audit;

/// <summary>
/// Per-request audit context (scoped: one instance per HTTP request). It collects, while the request runs, everything
/// the single audit record needs (R-LOG-01, R-LOG-08), so the record reflects what really happened (R-LOG-04).
/// </summary>
/// <remarks>
/// <para><b>Who writes what</b> (each participant only calls its own methods):</para>
/// <list type="table">
/// <listheader><term>Participant</term><description>Calls</description></listheader>
/// <item><term>Security middleware (<c>Api/Middleware/</c>, seguridad, F2)</term>
/// <description><see cref="Start"/> at the beginning of every request to <c>POST /api/asistente/preguntar</c>, with the
/// correlation id it reuses or creates (R-LOG-09) and <see cref="Clock.IClock.UtcNow"/>; at the end, <see cref="BuildRecord"/>
/// with the real HTTP status, and then <see cref="IAuditLog.WriteAsync"/> <b>exactly once</b> per request, also for 400,
/// 403 and 503 (R-LOG-01).</description></item>
/// <item><term>Inbound adapter (<c>Api/Endpoints/AssistantEndpoint</c>, nucleo)</term>
/// <description><see cref="SetRequest"/> after reading the body, because it is the only one that parses it: question as
/// received (D-27) and declared user. A malformed body records what could be read (possibly nothing).</description></item>
/// <item><term>Code that calls the simulated API (identity check, barrier list, tools; nucleo and seguridad)</term>
/// <description><see cref="RecordResource"/> for <b>every</b> route it requests (from <see cref="AcademicApiRoutes"/>), right
/// before calling the port, even if the call then fails; and <see cref="RecordStudentConsulted"/> only for
/// <c>/estudiantes/{id}</c>, <c>/notas</c> and <c>/asistencia</c> (R-LOG-06). Listing a program (D-24) and the calls made
/// to authorize, validate the identity or build the barrier list never record a student. The caller records, not the
/// HTTP adapter: the adapter does not know why a call is made.</description></item>
/// <item><term>Tool dispatcher of the use case (nucleo)</term>
/// <description><see cref="RecordTool"/> for each tool invocation the model requests, <c>rechazar</c> included (R-ARQ-04).</description></item>
/// <item><term>Use case (<c>AssistantService</c>, nucleo)</term>
/// <description><see cref="Complete"/> once, with its final <see cref="AssistantAnswer"/>: reason, citations, tokens and
/// cost (R-LOG-10, R-RES-05).</description></item>
/// </list>
/// <para><b>Implemented by:</b> <c>seguridad</c>, in <c>TutorFlow.Application/Audit/</c>, registered as scoped. It never
/// stores grades, absences, averages, names or the answer text (R-LOG-02).</para>
/// </remarks>
public interface IRequestAuditContext
{
    /// <summary>Starts the context. Called by the security middleware.</summary>
    /// <param name="correlationId">Value of the <c>X-Correlation-Id</c> response header (R-LOG-09).</param>
    /// <param name="startedAt">Start instant (UTC), used for <c>timestamp</c> and <c>duracionMs</c>.</param>
    void Start(string correlationId, DateTimeOffset startedAt);

    /// <summary>Records the question and the user as declared in the body. Called by the inbound adapter.</summary>
    /// <param name="question">Question as received, not trimmed nor masked (D-27); <see langword="null"/> if absent.</param>
    /// <param name="user">Declared role and id; their fields are <see langword="null"/> if absent.</param>
    void SetRequest(string? question, AuditUser user);

    /// <summary>
    /// Records a route requested to the simulated API (<c>recursosConsultados</c>). Distinct values, in first-call order.
    /// </summary>
    /// <param name="route">Route built with <see cref="AcademicApiRoutes"/>.</param>
    void RecordResource(string route);

    /// <summary>
    /// Records a student whose <c>/estudiantes/{id}</c>, <c>/notas</c> or <c>/asistencia</c> was requested
    /// (<c>estudiantesConsultados</c>, R-LOG-06). Distinct values, in first-call order.
    /// </summary>
    /// <param name="studentId">The consulted student.</param>
    void RecordStudentConsulted(StudentId studentId);

    /// <summary>Records a tool invoked by the model (<c>herramientas</c>), in order, repetitions included.</summary>
    /// <param name="toolName">Tool name, e.g. <c>rechazar</c>.</param>
    void RecordTool(string toolName);

    /// <summary>Records the outcome of the use case. Called once by the use case.</summary>
    /// <param name="answer">Final answer: provides <c>motivo</c>, <c>citas</c>, <c>tokens</c> and <c>costoUsd</c>.</param>
    void Complete(AssistantAnswer answer);

    /// <summary>
    /// Builds the audit record from everything recorded. Called once by the security middleware, which then writes it
    /// with <see cref="IAuditLog.WriteAsync"/>. If <see cref="Complete"/> was never called (e.g. an unexpected error), the
    /// record keeps empty citations, 0 tokens and 0 cost, and the reason the middleware decides.
    /// </summary>
    /// <param name="status">HTTP status actually returned (<c>status</c>).</param>
    /// <param name="finishedAt">End instant (UTC), for <c>duracionMs</c>.</param>
    /// <returns>The record with the fields of R-LOG-08.</returns>
    AuditRecord BuildRecord(int status, DateTimeOffset finishedAt);
}
