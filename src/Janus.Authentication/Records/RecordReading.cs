namespace Janus.Authentication.Records;

/// <summary>
/// A person record's read, with the name of the organization it is kept in.
/// </summary>
/// <param name="Record">The record.</param>
/// <param name="OrganizationName">The organization's name.</param>
/// <remarks>Implements REC-ACC-002, chapter 09 section 7a.</remarks>
internal sealed record RecordReading(ReadRecord Record, string OrganizationName);
