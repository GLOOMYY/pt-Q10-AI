namespace TutorFlow.Application.Ports.Audit;

/// <summary>Closed catalog of <c>motivo</c> values, with their exact texts (R-LOG-10).</summary>
public static class AuditReasons
{
    /// <summary>403 from the deterministic name and id barrier, before the LLM (R-SEG-14).</summary>
    public const string DeterministicBarrier = "barrera determinista";

    /// <summary>403 because data of others was requested (aggregates, mixed questions, other program, coordinators…).</summary>
    public const string ThirdPartyData = "datos de terceros";

    /// <summary>403 because a prompt injection attempt was detected.</summary>
    public const string InjectionAttempt = "intento de inyección";

    /// <summary>403 for asking for the prompt, the tools, the configuration or secrets (R-SEG-13).</summary>
    public const string SystemExtractionAttempt = "intento de extracción del sistema";

    /// <summary>403 for an invalid identity (R-SEG-04).</summary>
    public const string InvalidIdentity = "identidad inválida";

    /// <summary>400: the body does not follow the contract (SPEC §5.0).</summary>
    public const string InvalidRequest = "solicitud inválida";

    /// <summary>503: the simulated API or the LLM is not available.</summary>
    public const string DependencyUnavailable = "dependencia no disponible";

    /// <summary>All values of the catalog.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        DeterministicBarrier,
        ThirdPartyData,
        InjectionAttempt,
        SystemExtractionAttempt,
        InvalidIdentity,
        InvalidRequest,
        DependencyUnavailable,
    ];
}
