using System;

namespace Janus.Core;

/// <summary>
/// One consent as it stands: never a boolean, and never deleted.
/// </summary>
/// <param name="Purpose">Which purpose it was given for, and only one.</param>
/// <param name="NoticeVersion">The version of the privacy notice displayed when it was given.</param>
/// <param name="Mechanism">Where it was given.</param>
/// <param name="Kind">Whether the ordinary or the written path captured it.</param>
/// <param name="GrantedAt">When it was given.</param>
/// <param name="WithdrawnAt">When it was taken back, where it was.</param>
/// <param name="SupersededAt">
/// When a material revision of the notice ended it, where one did. A superseded
/// consent prompts and never blocks (PRIV-CONS-007).
/// </param>
/// <remarks>
/// Implements PRIV-CONS-001, PRIV-CONS-002, PRIV-CONS-004, PRIV-CONS-007 and
/// PRIV-CONS-008. Withdrawal sets a timestamp: the record is the evidence the law
/// asks for and is kept for <c>retention.consent</c> whatever the subject decides.
/// </remarks>
public sealed record ConsentRecord(
    string Purpose,
    string NoticeVersion,
    ConsentMechanism Mechanism,
    ConsentKind Kind,
    DateTimeOffset GrantedAt,
    DateTimeOffset? WithdrawnAt,
    DateTimeOffset? SupersededAt)
{
    /// <summary>
    /// Whether this consent is a lawful basis now: given, not taken back and not
    /// ended by a material revision of the notice.
    /// </summary>
    public bool Live => WithdrawnAt is null && SupersededAt is null;
}
