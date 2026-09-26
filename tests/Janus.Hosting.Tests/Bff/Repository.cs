using System.IO;
using System.Linq;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// Where the browser boundary is written, as the repository lays it out.
/// </summary>
internal static class Repository
{
    /// <summary>
    /// Every file the boundary is made of.
    /// </summary>
    /// <returns>The paths.</returns>
    public static string[] Sources() => Directory.GetFiles(
        Path.Combine(Root(), "src", "Janus.Hosting", "Bff"),
        "*.cs",
        SearchOption.AllDirectories);

    /// <summary>
    /// The file of one type of the boundary.
    /// </summary>
    /// <param name="name">The type.</param>
    /// <returns>What it holds.</returns>
    public static string Source(string name) =>
        File.ReadAllText(Path.Combine(Root(), "src", "Janus.Hosting", "Bff", name + ".cs"));

    /// <summary>
    /// What a named file of the authentication area holds.
    /// </summary>
    /// <param name="path">The path under the area, folders separated by a slash.</param>
    /// <returns>What it holds.</returns>
    public static string Authentication(string path) => File.ReadAllText(
        Path.Combine(Root(), "src", "Janus.Authentication", Path.Combine(path.Split('/'))));

    /// <summary>
    /// What a named file of the hosting project holds.
    /// </summary>
    /// <param name="path">The path under the project, folders separated by a slash.</param>
    /// <returns>What it holds.</returns>
    public static string Hosting(string path) => File.ReadAllText(
        Path.Combine(Root(), "src", "Janus.Hosting", Path.Combine(path.Split('/'))));

    /// <summary>
    /// Every file of one shipped project, leaving out what the build writes.
    /// </summary>
    /// <param name="project">The project, named as its assembly is.</param>
    /// <returns>The paths.</returns>
    public static string[] Project(string project) =>
    [
        .. Directory
            .GetFiles(Path.Combine(Root(), "src", project), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(folder => folder is "bin" or "obj")),
    ];

    private static string Root()
    {
        var at = new DirectoryInfo(System.AppContext.BaseDirectory);

        while (at is not null && !Directory.Exists(Path.Combine(at.FullName, "src")))
        {
            at = at.Parent;
        }

        return at!.FullName;
    }
}
