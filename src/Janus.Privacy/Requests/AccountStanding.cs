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
/// <remarks>Implements IDN-ACCT-007 and IDN-LIFE-003.</remarks>
internal sealed record AccountStanding(
    AccountState State,
    DeletionOrigin? DeletingBy,
    DateTimeOffset? DeletingSince);
