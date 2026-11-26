namespace TutorFlow.Application.Audit;

/// <summary>Resolution of the <c>X-Correlation-Id</c> value (R-LOG-09).</summary>
public static class CorrelationId
{
    /// <summary>Name of the HTTP header, in requests and responses (R-LOG-09).</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>Longest incoming value that is reused; longer ones are replaced by a generated id.</summary>
    public const int MaxLength = 128;

    /// <summary>
    /// Reuses the id sent by the caller when it is reasonable (1 to <see cref="MaxLength"/> printable ASCII characters,
    /// no spaces), so evals can find their record (R-LOG-09). Otherwise a new id is generated. The restriction keeps
    /// the value safe to echo in a header and to write in a JSON line (no log forging).
    /// </summary>
    /// <param name="incoming">Value of the request header; <see langword="null"/> or empty if absent.</param>
    /// <returns>The id to use for the response header and the audit record.</returns>
    public static string Resolve(string? incoming)
    {
        var candidate = incoming?.Trim();
        return IsReasonable(candidate) ? candidate! : Guid.NewGuid().ToString("N");
    }

    private static bool IsReasonable(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return false;
        }

        foreach (var c in value)
        {
            // Printable ASCII without the space: 0x21..0x7E.
            if (c is < '!' or > '~')
            {
                return false;
            }
        }

        return true;
    }
}
