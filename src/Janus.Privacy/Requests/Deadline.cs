using System;

namespace Janus.Privacy.Requests;

/// <summary>
/// The three instants the working-day clock produces for one request.
/// </summary>
/// <param name="Due">The end of the sixth working day after submission.</param>
/// <param name="WarnAt">The start of the working day the Normal alert is due on.</param>
/// <param name="EscalateAt">Midnight at the head of the deadline day, when the High alert is due.</param>
/// <remarks>Implements PRIV-RIGHT-002 and D-153.</remarks>
internal readonly record struct Deadline(
    DateTimeOffset Due,
    DateTimeOffset WarnAt,
    DateTimeOffset EscalateAt);
