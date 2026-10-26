using System;
using System.Collections.Generic;
using Janus.Privacy.Exports;

namespace Janus.Privacy.Tests.Exports;

/// <summary>
/// The fault entry, keeping each contributor's fault it was given, so a test can read
/// back what the assembly kept and of whom.
/// </summary>
internal sealed class FaultEntryInMemory : IFaultEntry
{
    private readonly List<(string Contributor, Exception Fault)> _kept = [];

    /// <summary>
    /// Every fault kept, with the contributor it was kept of, in the order it was.
    /// </summary>
    public IReadOnlyList<(string Contributor, Exception Fault)> Kept => _kept;

    /// <inheritdoc/>
    public void Keep(string contributor, Exception fault) => _kept.Add((contributor, fault));
}
