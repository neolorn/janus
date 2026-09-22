using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One group of an export: everything the library holds of one kind about the
/// subject.
/// </summary>
/// <param name="Name">
/// What the group is called, which is a stable name and never a sentence.
/// </param>
/// <param name="Records">What the group holds, in the order it is read in.</param>
/// <remarks>
/// Implements PRIV-RIGHT-003. A section with nothing in it is still present, so a
/// reader can tell what the library holds nothing of from what it never offered.
/// </remarks>
public sealed record ExportSection(string Name, IReadOnlyList<ExportRecord> Records);
