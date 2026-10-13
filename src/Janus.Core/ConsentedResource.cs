namespace Janus.Core;

/// <summary>
/// One row of the consented resources: a record of the host's whose data subject holds
/// a consent that is neither withdrawn nor superseded, with the purpose it was given
/// for, the document it was recorded against and its kind.
/// </summary>
/// <param name="ResourceType">The kind of thing the record is.</param>
/// <param name="ResourceId">The record, as the host names it.</param>
/// <param name="Purpose">The purpose the consent was given for.</param>
/// <param name="Document">The document the consent was recorded against.</param>
/// <param name="Kind">The kind of consent, <c>ordinary</c> or <c>written</c>.</param>
/// <remarks>
/// Implements AUTHZ-GATE-002, PRIV-SENS-002 and LIB-API-001. The view is public
/// contract: a host maps it into its own context with <c>MapAuthorizationTables</c>,
/// and a filter for a permission bound to a consent-based purpose reads it there. A
/// data subject holds one live consent a purpose, so a record and a purpose name one
/// row. The columns are plain values rather than the library's own types, because what
/// maps this row is the host's provider and not the library's (D-159).
/// </remarks>
public sealed record ConsentedResource(
    string ResourceType,
    string ResourceId,
    string Purpose,
    string Document,
    string Kind);
