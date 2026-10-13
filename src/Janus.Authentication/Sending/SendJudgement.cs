using System.Collections.Generic;

namespace Janus.Authentication.Sending;

/// <summary>
/// What an admitted send counts against and spends, as the named restrictions judged
/// it: each of its messages against every restriction not marked <c>resend</c>, and the
/// send once against each restriction that is.
/// </summary>
/// <param name="Messages">What each message counts against and spends, in the order they are admitted.</param>
/// <param name="Once">What the send counts against and spends once, whatever the number of its messages.</param>
/// <param name="SecondFactor">
/// Whether the send's purpose is <c>secondfactor</c>, which lets a report that one of
/// its texts was delivered release what it counts once.
/// </param>
/// <remarks>Implements AUTH-ABUSE-004 and chapter 10 section 5.16a.</remarks>
internal sealed record SendJudgement(
    IReadOnlyList<SendPlan> Messages,
    SendPlan Once,
    bool SecondFactor);
