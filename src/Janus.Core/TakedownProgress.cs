using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// Where a takedown has got: the hosts' confirmations of the delivery its trigger
/// wrote, and when its erasure runs.
/// </summary>
/// <param name="Id">What the takedown is held under.</param>
/// <param name="Subject">Whose account was taken down.</param>
/// <param name="TriggeredAt">When phase one committed.</param>
/// <param name="ErasureDue">When the grace window ends and the erasure runs.</param>
/// <param name="Status">How far the subscribers have got.</param>
/// <param name="Attempts">How many delivery attempts have been made across them.</param>
/// <param name="Subscribers">Each registered subscriber and whether it confirmed.</param>
/// <remarks>
/// Implements IDN-LIFE-003 AC2 and IDN-LIFE-003a: the completion of the hosts' half is
/// readable from the moment of the trigger, and the erasure's own progress is the
/// erasures row once phase two has run.
/// </remarks>
public sealed record TakedownProgress(
    TakedownId Id,
    SubjectId Subject,
    DateTimeOffset TriggeredAt,
    DateTimeOffset ErasureDue,
    ErasureStatus Status,
    int Attempts,
    IReadOnlyList<SubscriberConfirmation> Subscribers);
