namespace Janus.Core;

/// <summary>
/// One of the host's records as the host tells the library about it.
/// </summary>
/// <param name="Resource">Which record.</param>
/// <param name="Organization">The organization owning it, which every evaluation on it is scoped to.</param>
/// <param name="ContainedIn">What contains it, where anything does.</param>
/// <param name="Subject">
/// Whose data it is, read from the column its type declares for its encrypted fields,
/// where it is anybody's.
/// </param>
/// <remarks>Implements AUTHZ-INHERIT-002, AUTHZ-SCOPE-001 and PRIV-SENS-002.</remarks>
public sealed record ResourceRegistration(
    ResourceReference Resource,
    OrganizationId Organization,
    ResourceReference? ContainedIn,
    SubjectId? Subject);
