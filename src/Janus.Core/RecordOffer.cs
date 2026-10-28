using System;

namespace Janus.Core;

/// <summary>
/// What the latest proof of an account's email or phone offers it to attach: how many
/// person records, and until when, and nothing of the records themselves.
/// </summary>
/// <param name="Count">How many records the offer counts.</param>
/// <param name="ExpiresAt">When the offer ends, or nothing where no offer stands.</param>
/// <remarks>Implements REC-ATT-001 and chapter 09 section 7a.</remarks>
public sealed record RecordOffer(int Count, DateTimeOffset? ExpiresAt)
{
    /// <summary>
    /// The answer where no offer stands.
    /// </summary>
    public static RecordOffer None { get; } = new(0, null);
}
