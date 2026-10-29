using System;
using Janus.Core;

namespace Janus.Privacy.Erasures;

/// <summary>
/// An account or a person record standing in its deletion grace window: whose it is,
/// what began it, and when the window started running.
/// </summary>
/// <param name="Subject">Whose account it is.</param>
/// <param name="By">What began the deletion.</param>
/// <param name="Since">When the window began.</param>
/// <param name="HeldSince">
/// When the deletion a takedown found running began, where it found one.
/// </param>
/// <remarks>
/// Implements IDN-LIFE-014, IDN-LIFE-003 and REC-REC-003. What began the deletion decides the
/// reason the erasure carries to the subscribers, so the sweep reads it here rather
/// than assuming every window was the subject's own doing.
/// </remarks>
internal sealed record PendingDeletion(
    SubjectId Subject,
    DeletionOrigin By,
    DateTimeOffset Since,
    DateTimeOffset? HeldSince)
{
    /// <summary>
    /// Whether the subject is a person record's, whose own deletion runs
    /// <c>record.deletion.grace</c> (REC-REC-002).
    /// </summary>
    public bool IsPersonRecord { get; init; }
}
