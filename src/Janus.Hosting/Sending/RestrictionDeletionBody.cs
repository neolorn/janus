namespace Janus.Hosting.Sending;

/// <summary>
/// What deleting a restriction carries, as the request reads.
/// </summary>
/// <param name="Reason">Why, which the loosening a deletion is requires.</param>
/// <remarks>Implements chapter 09 section 8, AUTH-ABUSE-004 and OPS-CFG-002.</remarks>
internal sealed record RestrictionDeletionBody(string? Reason);
