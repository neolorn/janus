using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Authentication.Records;

namespace Janus.Hosting.Records;

/// <summary>
/// The state of a record access session, as <c>GET /records/access</c> answers it.
/// </summary>
/// <param name="ExpiresAt">When the session stops answering.</param>
/// <param name="Records">The records it reaches.</param>
/// <remarks>Implements REC-ACC-001, chapter 09 section 7a.</remarks>
internal sealed record RecordAccessStateView(DateTimeOffset ExpiresAt, IReadOnlyList<AccessibleRecordView> Records)
{
    /// <summary>
    /// Shows a session's state.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <returns>Its view.</returns>
    /// <exception cref="ArgumentNullException">The state is absent.</exception>
    public static RecordAccessStateView Of(RecordAccessState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new RecordAccessStateView(state.ExpiresAt, [.. state.Records.Select(AccessibleRecordView.Of)]);
    }
}
