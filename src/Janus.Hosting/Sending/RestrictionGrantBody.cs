namespace Janus.Hosting.Sending;

/// <summary>
/// What granting credit under a restriction carries, as the request reads.
/// </summary>
/// <param name="KeyValue">The plain address, account, source or host value.</param>
/// <param name="Credit">How many sends the credit is worth.</param>
/// <param name="Reason">Why, which every grant requires.</param>
/// <remarks>Implements chapter 09 section 8 and AUTH-ABUSE-004.</remarks>
internal sealed record RestrictionGrantBody(string? KeyValue, int? Credit, string? Reason);
