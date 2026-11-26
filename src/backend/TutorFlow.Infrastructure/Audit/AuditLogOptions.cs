namespace TutorFlow.Infrastructure.Audit;

/// <summary>Settings of the audit log adapter (R-LOG-07, DUDA-06).</summary>
public sealed class AuditLogOptions
{
    /// <summary>Optional configuration key that overrides <see cref="FilePath"/>.</summary>
    public const string FilePathKey = "AUDIT_LOG_PATH";

    /// <summary>Default path, relative to the working directory: <c>/app/var/auditoria/auditoria.jsonl</c> in the container (R-LOG-07).</summary>
    public const string DefaultFilePath = "var/auditoria/auditoria.jsonl";

    /// <summary>File the records are appended to, one JSON line each.</summary>
    public string FilePath { get; set; } = DefaultFilePath;
}
