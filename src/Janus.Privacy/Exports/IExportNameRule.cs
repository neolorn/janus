namespace Janus.Privacy.Exports;

/// <summary>
/// Where the export's assembly asks whether a name an export contributor answers, a
/// section's or a value's, keeps the rule the machine format keys it by.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-003, INT-SMS-003 and CONV-DESIGN-007 (D-206). The rule's check
/// is internal to <c>Janus.Core</c>, so <c>Janus.Hosting</c>, which registers the
/// contributors, answers it; a name outside the rule could make two keys of
/// <c>&lt;section&gt;.&lt;index&gt;.&lt;value&gt;</c> coincide.
/// </remarks>
internal interface IExportNameRule
{
    /// <summary>
    /// Whether one name keeps the rule: 1 to 64 lower-case letters and digits separated
    /// by single <c>-</c> or <c>_</c>.
    /// </summary>
    /// <param name="name">The name, or nothing.</param>
    /// <returns>Whether it does; an absent name does not.</returns>
    bool Holds(string? name);
}
