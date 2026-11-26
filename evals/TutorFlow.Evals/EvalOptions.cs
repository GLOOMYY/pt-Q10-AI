using System.Globalization;

namespace TutorFlow.Evals;

/// <summary>Command line options of the evals (README of <c>evals/</c>).</summary>
public sealed class EvalOptions
{
    /// <summary>Base URL of the system under test (the backend of <c>docker compose</c>, R-DEP-01).</summary>
    public Uri BaseUrl { get; private set; } = new("http://localhost:8080");

    /// <summary>Path of the audit file (README section 5.1); relative to the repository root.</summary>
    public string AuditFile { get; private set; } = Path.Combine("var", "auditoria", "auditoria.jsonl");

    /// <summary>Directory of the reports; relative to the repository root. <c>var/</c> is ignored by git.</summary>
    public string ReportDirectory { get; private set; } = Path.Combine("var", "evals");

    /// <summary>Only these scenario ids, when not empty.</summary>
    public IReadOnlyList<string> Only { get; private set; } = [];

    /// <summary>Only the scenarios of these files (e.g. <c>banco.md</c>), when not empty.</summary>
    public IReadOnlyList<string> Files { get; private set; } = [];

    /// <summary>Pause between scenarios.</summary>
    public TimeSpan Delay { get; private set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Timeout of each request (the real LLM can be slow).</summary>
    public TimeSpan RequestTimeout { get; private set; } = TimeSpan.FromSeconds(120);

    /// <summary>Only parse and list the scenarios; no network, no key needed.</summary>
    public bool ListOnly { get; private set; }

    /// <summary>Show the help and exit.</summary>
    public bool Help { get; private set; }

    /// <summary>Parses the arguments.</summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>The options.</returns>
    /// <exception cref="ArgumentException">An unknown option or a value that cannot be read.</exception>
    public static EvalOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var options = new EvalOptions();
        for (var i = 0; i < args.Length; i++)
        {
            string Value() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Missing value for {args[i]}.");
            switch (args[i])
            {
                case "--base-url":
                    options.BaseUrl = new Uri(Value(), UriKind.Absolute);
                    break;
                case "--audit-file":
                    options.AuditFile = Value();
                    break;
                case "--report-dir":
                    options.ReportDirectory = Value();
                    break;
                case "--only":
                    options.Only = Split(Value());
                    break;
                case "--file":
                    options.Files = Split(Value());
                    break;
                case "--delay-ms":
                    options.Delay = TimeSpan.FromMilliseconds(int.Parse(Value(), CultureInfo.InvariantCulture));
                    break;
                case "--timeout-s":
                    options.RequestTimeout = TimeSpan.FromSeconds(int.Parse(Value(), CultureInfo.InvariantCulture));
                    break;
                case "--list":
                    options.ListOnly = true;
                    break;
                case "--help" or "-h":
                    options.Help = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        return options;
    }

    /// <summary>Usage text.</summary>
    public const string Usage = """
        TutorFlow evals: api scenarios against the running system (docker compose) with the REAL LLM.

        The stack must be up (docker compose) with the real LLM key configured in its .env; a probe question checks it before running.

        Options:
          --base-url <url>      System under test. Default http://localhost:8080
          --audit-file <path>   Audit file. Default var/auditoria/auditoria.jsonl (from the repository root)
          --report-dir <path>   Report directory. Default var/evals (ignored by git)
          --only <ids>          Comma separated scenario ids, e.g. ESC-BAN-01,ESC-BAN-02
          --file <files>        Comma separated scenario files, e.g. banco.md,citas.md
          --delay-ms <n>        Pause between scenarios. Default 500
          --timeout-s <n>       Timeout of each request. Default 120
          --list                Only parse and list the scenarios (no network, no key)
          --help                This text
        """;

    private static string[] Split(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
