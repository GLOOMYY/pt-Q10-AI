namespace TutorFlow.Infrastructure.AcademicApi;

/// <summary>Settings of the simulated academic API client (R-DEP-02, R-QA-04).</summary>
public sealed class AcademicApiOptions
{
    /// <summary>Configuration key of the base URL.</summary>
    public const string BaseUrlKey = "MOCK_API_URL";

    /// <summary>Configuration key of the timeout in seconds (optional).</summary>
    public const string TimeoutSecondsKey = "MOCK_API_TIMEOUT_SECONDS";

    /// <summary>Default base URL, as seen from the backend inside Docker (R-DEP-02).</summary>
    public const string DefaultBaseUrl = "http://mock-api:4000";

    /// <summary>Default timeout: 5 seconds (R-QA-04).</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Base URL of the simulated API (<c>MOCK_API_URL</c>).</summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;

    /// <summary>Per-request timeout (<c>MOCK_API_TIMEOUT_SECONDS</c>, default 5 s).</summary>
    public TimeSpan Timeout { get; set; } = DefaultTimeout;
}
