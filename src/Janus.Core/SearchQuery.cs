namespace Janus.Core;

/// <summary>
/// What a staff search is asked: in which organization, for what, for which kinds, and
/// for at most how many results.
/// </summary>
/// <param name="Organization">The organization the search is asked in.</param>
/// <param name="Text">The query as typed, which is never logged.</param>
/// <param name="Kinds">The kinds asked for, <see cref="SearchKinds.None"/> meaning both.</param>
/// <param name="Limit">At most how many results, 1 to 50.</param>
/// <remarks>Implements IDN-SRCH-001, IDN-SRCH-002, LIB-API-005 and CONV-LOG-003 (D-256).</remarks>
public sealed record SearchQuery(
    OrganizationId Organization,
    [property: NeverLogged] string Text,
    SearchKinds Kinds,
    int Limit);
