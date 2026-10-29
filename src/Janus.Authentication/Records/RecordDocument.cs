using System;

namespace Janus.Authentication.Records;

/// <summary>
/// One version of a document a collection recorded for a person record: a notice
/// presented, or terms accepted.
/// </summary>
/// <param name="Version">The version.</param>
/// <param name="At">When it was presented or accepted.</param>
/// <remarks>Implements REC-ACC-002, REC-COL-002, chapter 09 section 7a.</remarks>
internal sealed record RecordDocument(string Version, DateTimeOffset At);
