namespace Janus.Authorization.Gate;

/// <summary>
/// One record of a page that one derivation admits, as the host's own query answers it.
/// </summary>
/// <param name="Resource">The record.</param>
/// <param name="Ancestor">
/// The container the relationship names, which is the record itself where the
/// derivation sits on its own type.
/// </param>
/// <param name="Relationship">The relationship whose derivation admitted it.</param>
/// <remarks>
/// Implements AUTHZ-DERIVE-001, AUTHZ-GATE-004 and AUTHZ-GATE-005 AC1. The relationship
/// travels with the record so that what its derivation confers is mapped where the
/// model is, rather than asked for in a query of its own per permission, and the
/// container travels with it so that an explanation can name what the access was
/// inherited from.
/// </remarks>
internal sealed record AdmittedRecord(string Resource, string Ancestor, string Relationship);
