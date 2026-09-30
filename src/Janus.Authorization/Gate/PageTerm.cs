namespace Janus.Authorization.Gate;

/// <summary>
/// Whether one term of the rule holds on one record of a page, as the host's own query
/// answers it.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GATE-002 AC1, AUTHZ-GATE-005 AC1 and AUTHZ-DERIVE-001 (D-166). Each
/// term is the one the filter composes, so the page decides what the filter decides;
/// what a derivation's role allows is model data and is mapped where the model is.
/// </remarks>
internal sealed class PageTerm
{
    /// <summary>
    /// The term of the stored allow grants of one permission.
    /// </summary>
    public const string Allowing = "allow";

    /// <summary>
    /// The term of the stored deny grants of one permission.
    /// </summary>
    public const string Denying = "deny";

    /// <summary>
    /// The term of one derivation.
    /// </summary>
    public const string Deriving = "derived";

    /// <summary>
    /// The record.
    /// </summary>
    public string Resource { get; init; } = string.Empty;

    /// <summary>
    /// Which term: <see cref="Allowing"/> or <see cref="Denying"/> for the stored grants
    /// of one permission, <see cref="Deriving"/> for one derivation.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// The permission, or the relationship whose derivation the term is.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Whether the term holds on the record.
    /// </summary>
    public bool Holds { get; init; }
}
