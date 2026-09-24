using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// Where an erasure's host-side work has got: each registered subscriber and whether it
/// confirmed.
/// </summary>
/// <param name="Id">What the erasure is held under.</param>
/// <param name="Subject">Whose erasure it is.</param>
/// <param name="Reason">Why it ran.</param>
/// <param name="Status">How far the subscribers have got.</param>
/// <param name="Attempts">How many delivery attempts have been made across them.</param>
/// <param name="Subscribers">Each registered subscriber and whether it confirmed.</param>
/// <remarks>
/// Implements IDN-LIFE-003a and IDN-LIFE-003b. The library's own steps committed with
/// the erasure, so what is read here is only what the hosts still owe.
/// </remarks>
public sealed record ErasureProgress(
    ErasureId Id,
    SubjectId Subject,
    ErasureReason Reason,
    ErasureStatus Status,
    int Attempts,
    IReadOnlyList<SubscriberConfirmation> Subscribers);
