using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// Where the deployment's secrets are not: the repository carries none of them, in any
/// file a deployment would read at startup (AUTH-KEY-002).
/// </summary>
[Trait("kind", "unit")]
public sealed partial class SecretMaterialTests
{
    private static readonly string[] Configuration =
        ["*.json", "*.yml", "*.yaml", "*.props", "*.targets", "*.config", "*.ini", "*.toml", "*.env"];

    private static readonly string[] NotConfiguration =
        ["bin", "obj", "tmp", ".git", "node_modules"];

    /// <summary>
    /// AUTH-KEY-002 AC1: no configuration file in the repository names a secret and
    /// gives it a value; what the deployment holds comes from the secrets manager and
    /// is written down nowhere here.
    /// </summary>
    [Fact]
    public void AUTH_KEY_002_AC1_NoConfigurationFileCarriesASecretValue() =>
        Assert.Empty(Carrying());

    [GeneratedRegex(
        """(?<name>secret|password|passphrase|api[-_]?key|private[-_]?key|connection[-_]?string|access[-_]?token)\s*[:=]\s*"?(?<value>[^",\s#}]+)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Assignment { get; }

    // A reference the deployment resolves elsewhere is not a value: the workflow's
    // ${{ ... }}, a build variable, an environment placeholder, or an empty entry.
    private static bool Resolved(string value) =>
        value.Length is 0
        || value.StartsWith("${{", StringComparison.Ordinal)
        || value.StartsWith("$(", StringComparison.Ordinal)
        || value.StartsWith('%')
        || value.StartsWith("null", StringComparison.OrdinalIgnoreCase);

    private static List<string> Carrying()
    {
        var carrying = new List<string>();

        foreach (string file in Files())
        {
            foreach (string line in File.ReadLines(file))
            {
                Match found = Assignment.Match(line);

                if (found.Success && !Resolved(found.Groups["value"].Value))
                {
                    carrying.Add(Path.GetRelativePath(Repository.Root, file) + ": " + line.Trim());
                }
            }
        }

        return carrying;
    }

    private static IEnumerable<string> Files() =>
        Configuration
            .SelectMany(pattern =>
                Directory.EnumerateFiles(Repository.Root, pattern, SearchOption.AllDirectories))
            .Where(file => !Path.GetRelativePath(Repository.Root, file)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => NotConfiguration.Contains(part, StringComparer.Ordinal)));
}
