using System;
using Janus.Core;
using Janus.Privacy.Exports;

namespace Janus.Hosting.Privacy;

/// <summary>
/// The rule a name an export contributor answers is held to: the rule of a message
/// place, without <c>.</c>.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-003, INT-SMS-003 and CONV-DESIGN-007 (D-206). The machine
/// format keys a value <c>&lt;section&gt;.&lt;index&gt;.&lt;value&gt;</c>, so a name
/// holding a <c>.</c> could make two keys coincide; the rest of the rule is the one a
/// subscriber's and a purpose handler's names are judged by here.
/// </remarks>
internal sealed class ExportNames : IExportNameRule
{
    /// <inheritdoc/>
    public bool Holds(string? name) =>
        name is not null && PlaceName.Holds(name) && !name.Contains('.', StringComparison.Ordinal);
}
