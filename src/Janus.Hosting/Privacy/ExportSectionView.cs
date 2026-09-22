using System.Collections.Generic;

namespace Janus.Hosting.Privacy;

/// <summary>
/// One labelled group of the readable arrangement: the records of one part of what
/// is held, in the order they are read in.
/// </summary>
/// <param name="Name">
/// What the group is called, which the frontend localizes; no sentence crosses here
/// (CONV-CONTENT-001).
/// </param>
/// <param name="Records">The records of the group, each a set of named values.</param>
/// <remarks>Implements PRIV-RIGHT-003 and D-058.</remarks>
internal sealed record ExportSectionView(
    string Name,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Records);
