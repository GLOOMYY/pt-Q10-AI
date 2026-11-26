namespace TutorFlow.Application.Ports;

/// <summary>External dependency that an outbound adapter wraps.</summary>
public enum ExternalDependency
{
    /// <summary>Not specified (only used by the parameterless constructors of the exception).</summary>
    Unspecified = 0,

    /// <summary>The simulated academic API (<c>material/mock-api</c>).</summary>
    AcademicApi = 1,

    /// <summary>The language model provider.</summary>
    Llm = 2,
}

/// <summary>
/// Thrown by an outbound adapter when its dependency cannot give a trustworthy answer: connection refused,
/// timeout (R-QA-04: 5 s for the simulated API, 30 s for the LLM), quota exhausted (e.g. HTTP 429, CL-23),
/// 5xx responses or an unreadable payload.
/// </summary>
/// <remarks>
/// The use case maps it to <c>503</c> with the standard body (SPEC §5.0, API-02) and never invents data.
/// It is deliberately distinct from "not found", which ports report with a <see langword="null"/> result.
/// </remarks>
public sealed class DependencyUnavailableException : Exception
{
    /// <summary>Creates the exception without details.</summary>
    public DependencyUnavailableException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">Technical description (English, never shown to the user).</param>
    public DependencyUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">Technical description (English, never shown to the user).</param>
    /// <param name="innerException">The underlying failure.</param>
    public DependencyUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception for a known dependency.</summary>
    /// <param name="dependency">Which dependency failed.</param>
    /// <param name="message">Technical description (English, never shown to the user).</param>
    /// <param name="innerException">The underlying failure, if any.</param>
    public DependencyUnavailableException(ExternalDependency dependency, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Dependency = dependency;
    }

    /// <summary>Which dependency failed.</summary>
    public ExternalDependency Dependency { get; }
}
