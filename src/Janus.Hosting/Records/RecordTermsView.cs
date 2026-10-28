using System;

namespace Janus.Hosting.Records;

/// <summary>
/// A terms version a person record's collection recorded as accepted.
/// </summary>
/// <param name="Version">The version.</param>
/// <param name="AcceptedAt">When it was accepted.</param>
/// <remarks>Implements REC-ACC-002, chapter 09 section 7a.</remarks>
internal sealed record RecordTermsView(string Version, DateTimeOffset AcceptedAt);
