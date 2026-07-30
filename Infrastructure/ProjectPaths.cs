namespace InterviewPrep.Infrastructure;

/// <summary>
/// Resolves the project root reliably by walking up from the build output
/// directory until a folder containing a .csproj file is found. This works no
/// matter how the process was launched (dotnet run, published exe, etc.).
/// </summary>
public static class ProjectPaths
{
    public static string ProjectRoot { get; } = ResolveProjectRoot();

    private static string ResolveProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 6 && dir is not null; i++)
        {
            if (dir.GetFiles("*.csproj").Length > 0)
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        // Published exe (no .csproj in any parent): use the folder next to the
        // exe, where appsettings.json is copied on publish.
        return AppContext.BaseDirectory;
    }
}
