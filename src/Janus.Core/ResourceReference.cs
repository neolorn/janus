namespace Janus.Core;

/// <summary>
/// One of the host's records: a kind of thing and which one. A grant naming none is
/// on the whole organization; a resource naming none is contained in nothing.
/// </summary>
/// <param name="Type">The kind of thing.</param>
/// <param name="Id">The record.</param>
/// <remarks>Implements AUTHZ-GRANT-001, AUTHZ-INHERIT-001 and CONV-DESIGN-004.</remarks>
public readonly record struct ResourceReference(ResourceType Type, ResourceId Id);
