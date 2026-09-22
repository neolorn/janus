using System;

namespace Janus.Core;

/// <summary>
/// One objection as it stands, recorded as a consent is.
/// </summary>
/// <param name="Purpose">Which purpose it was recorded against, and only one.</param>
/// <param name="NoticeVersion">The version of the privacy notice displayed when it was recorded.</param>
/// <param name="Mechanism">Where it was recorded.</param>
/// <param name="RecordedAt">When it was recorded.</param>
/// <param name="WithdrawnAt">When it was taken back, where it was.</param>
/// <remarks>
/// Implements PRIV-RIGHT-001a. An objection is always honoured; nothing here carries
/// a ground on which it could be refused.
/// </remarks>
public sealed record ObjectionRecord(
    string Purpose,
    string NoticeVersion,
    ConsentMechanism Mechanism,
    DateTimeOffset RecordedAt,
    DateTimeOffset? WithdrawnAt)
{
    /// <summary>
    /// Whether the subject objects now.
    /// </summary>
    public bool Standing => WithdrawnAt is null;
}
