namespace Janus.Authorization.Gate;

/// <summary>
/// One record of a page that one derivation admits, as the host's own query answers it.
/// </summary>
/// <param name="Resource">The record.</param>
/// <param name="Relationship">The relationship whose derivation admitted it.</param>
/// <remarks>
/// Implements AUTHZ-DERIVE-001 and AUTHZ-GATE-005 AC1. The relationship travels with
/// the record so that what its derivation confers is mapped where the model is, rather
/// than asked for in a query of its own per permission.
/// </remarks>
internal sealed record AdmittedRecord(string Resource, string Relationship);
