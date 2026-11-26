using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace TutorFlow.Application.Ports.AcademicApi;

/// <summary>
/// Normalization shared by every academic identifier (API-05): trim, then upper-case with the invariant culture.
/// </summary>
internal static class AcademicIdNormalizer
{
    /// <summary>Returns the normalized identifier, or <see langword="null"/> when the input is null.</summary>
    public static string? Normalize(string? raw) => raw?.Trim().ToUpperInvariant();
}

/// <summary>
/// A validated student identifier (<c>EST-ddd</c>). It can only be built through <see cref="TryParse"/>, so any
/// value of this type is safe to put in a URL of the simulated API (API-01, API-05, R-SEG-04).
/// </summary>
public sealed partial record StudentId
{
    private StudentId(string value) => Value = value;

    /// <summary>The canonical value, e.g. <c>EST-001</c>.</summary>
    public string Value { get; }

    /// <summary>
    /// Normalizes (trim + upper case, API-05) and validates against <c>^EST-[0-9]{3}$</c> (API-01).
    /// </summary>
    /// <param name="raw">Raw text, e.g. <c>" est-001 "</c>.</param>
    /// <param name="id">The parsed identifier when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if the text is a well-formed student identifier.</returns>
    public static bool TryParse(string? raw, [NotNullWhen(true)] out StudentId? id)
    {
        var normalized = AcademicIdNormalizer.Normalize(raw);
        id = normalized is not null && Pattern().IsMatch(normalized) ? new StudentId(normalized) : null;
        return id is not null;
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    // ASCII digits only: \d would also accept other Unicode digits (API-01 asks for a strict pattern).
    [GeneratedRegex(@"^EST-[0-9]{3}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}

/// <summary>
/// A validated coordinator identifier (<c>COORD-dd</c>). Built only through <see cref="TryParse"/> (API-01).
/// </summary>
public sealed partial record CoordinatorId
{
    private CoordinatorId(string value) => Value = value;

    /// <summary>The canonical value, e.g. <c>COORD-01</c>.</summary>
    public string Value { get; }

    /// <summary>
    /// Normalizes (trim + upper case, API-05) and validates against <c>^COORD-[0-9]{2}$</c> (API-01).
    /// </summary>
    /// <param name="raw">Raw text.</param>
    /// <param name="id">The parsed identifier when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if the text is a well-formed coordinator identifier.</returns>
    public static bool TryParse(string? raw, [NotNullWhen(true)] out CoordinatorId? id)
    {
        var normalized = AcademicIdNormalizer.Normalize(raw);
        id = normalized is not null && Pattern().IsMatch(normalized) ? new CoordinatorId(normalized) : null;
        return id is not null;
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    [GeneratedRegex(@"^COORD-[0-9]{2}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}

/// <summary>
/// A validated program identifier (e.g. <c>ING-SIS</c>, <c>ADM</c>). Built only through <see cref="TryParse"/>
/// (API-01). Program identifiers returned by the API are validated too before being used in a route.
/// </summary>
public sealed partial record ProgramId
{
    private ProgramId(string value) => Value = value;

    /// <summary>The canonical value, e.g. <c>ING-SIS</c>.</summary>
    public string Value { get; }

    /// <summary>
    /// Normalizes (trim + upper case, API-05) and validates against <c>^[A-Z]+(-[A-Z]+)?$</c> (API-01).
    /// </summary>
    /// <param name="raw">Raw text.</param>
    /// <param name="id">The parsed identifier when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if the text is a well-formed program identifier.</returns>
    public static bool TryParse(string? raw, [NotNullWhen(true)] out ProgramId? id)
    {
        var normalized = AcademicIdNormalizer.Normalize(raw);
        id = normalized is not null && Pattern().IsMatch(normalized) ? new ProgramId(normalized) : null;
        return id is not null;
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    [GeneratedRegex(@"^[A-Z]+(-[A-Z]+)?\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
