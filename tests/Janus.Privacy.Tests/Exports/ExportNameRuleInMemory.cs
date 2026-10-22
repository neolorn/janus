using System;
using System.Collections.Generic;
using Janus.Privacy.Exports;

namespace Janus.Privacy.Tests.Exports;

/// <summary>
/// The rule the names a contributor answers are held to, refusing the names a test
/// gives it and keeping every name it was asked about, so a test can read back what
/// the assembly asked and when.
/// </summary>
internal sealed class ExportNameRuleInMemory : IExportNameRule
{
    private readonly List<string?> _asked = [];

    /// <summary>
    /// The names it refuses; every other name keeps the rule.
    /// </summary>
    public ISet<string> Refused { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// What a test does while a name is being judged.
    /// </summary>
    public Action? Meanwhile { get; set; }

    /// <summary>
    /// Every name it was asked about, in the order it was.
    /// </summary>
    public IReadOnlyList<string?> Asked => _asked;

    /// <inheritdoc/>
    public bool Holds(string? name)
    {
        _asked.Add(name);
        Meanwhile?.Invoke();

        return name is not null && !Refused.Contains(name);
    }
}
