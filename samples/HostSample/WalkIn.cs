using System.Collections.Generic;

namespace HostSample;

/// <summary>
/// A kite a guest leaves at the counter with no account, from the guest's screen once
/// the guest's contact is verified and age answered in the collection steps.
/// </summary>
/// <param name="Ticket">The ticket on the kite's tag, which the guest keeps as the collection's reference.</param>
/// <param name="Kite">What the kite is.</param>
/// <param name="ContactName">The name to contact the guest by, also the record's name.</param>
/// <param name="Need">What the guest's grip needs, for the fitting the guest consented to.</param>
/// <param name="NoticeVersion">The version of the privacy notice the screen presented.</param>
/// <param name="TermsVersion">The version of the terms the guest accepted.</param>
/// <param name="Consents">The consent-based purposes the guest ticked.</param>
public sealed record WalkIn(
    string Ticket,
    string Kite,
    string ContactName,
    string Need,
    string NoticeVersion,
    string? TermsVersion,
    IReadOnlyList<string> Consents);
