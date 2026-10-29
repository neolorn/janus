using System;
using System.Collections.Generic;

namespace Janus.Authentication.Records;

/// <summary>
/// What an open record access session shows: when it ends and the records it reaches.
/// </summary>
/// <param name="ExpiresAt">When the session stops answering.</param>
/// <param name="Records">The records it reaches, still in its reach.</param>
/// <remarks>Implements REC-ACC-001 and chapter 09 section 7a.</remarks>
internal sealed record RecordAccessState(DateTimeOffset ExpiresAt, IReadOnlyList<AccessibleRecord> Records);
