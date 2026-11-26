namespace TutorFlow.Scenarios;

/// <summary>Finds the repository files the runners read (they are never copied).</summary>
public static class RepositoryLocator
{
    /// <summary>Walks up from a directory until it finds the repository root (the directory with <c>escenarios/README.md</c>).</summary>
    /// <param name="start">Directory to start from; the application base directory when <see langword="null"/>.</param>
    /// <returns>The absolute path of the root.</returns>
    /// <exception cref="DirectoryNotFoundException">No root above <paramref name="start"/>.</exception>
    public static string FindRoot(string? start = null)
    {
        for (var dir = new DirectoryInfo(start ?? AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "escenarios", "README.md")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("escenarios/README.md was not found above " + (start ?? AppContext.BaseDirectory));
    }
}
