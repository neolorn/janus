using System;
using Janus.Core;

namespace Janus.Privacy.Requests;

/// <summary>
/// Where an account stands: its state, and what put it in its deletion window where
/// it is in one.
/// </summary>
/// <param name="State">Its state.</param>
/// <param name="DeletingBy">What started the window, where one was started.</param>
/// <param name="DeletingSince">When the window began, where one was started.</param>
/// <param name="DeletionHeldSince">
/// When the deletion a takedown found running began, where it found one.
/// </param>
/// <param name="Emergency">Whether it is the reserved emergency account.</param>
/// <remarks>Implements IDN-ACCT-007, IDN-LIFE-003, OPS-BOOT-002 and REC-ADM-001.</remarks>
internal sealed record AccountStanding(
    AccountState State,
    DeletionOrigin? DeletingBy,
    DateTimeOffset? DeletingSince,
    DateTimeOffset? DeletionHeldSince,
    bool Emergency)
{
    /// <summary>
    /// Whether the subject is a person record's rather than an account's (REC-ADM-001).
    /// </summary>
    public bool IsPersonRecord { get; init; }
}
