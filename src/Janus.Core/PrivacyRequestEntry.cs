using System;

namespace Janus.Core;

/// <summary>
/// A request that reached the company out of band, as the human entering it has it.
/// </summary>
/// <param name="Subject">Whose request it is.</param>
/// <param name="Type">What it asks for.</param>
/// <param name="Detail">What the request said.</param>
/// <param name="ReceivedAt">
/// The calendar date it reached the company, in <c>privacy.calendar.timezone</c>,
/// never later than today there. The decision clock runs from the end of it.
/// </param>
/// <param name="Channel">How it arrived: a letter, a support mail, a guardian.</param>
/// <param name="IdentityConfirmation">
/// What was done to confirm the requester is the subject, in the words of the human
/// who did it.
/// </param>
/// <remarks>
/// Implements PRIV-RIGHT-001, PRIV-RIGHT-002, chapter 09 section 8a, D-113 and
/// D-136. Every field is required: a request nobody can attribute to a subject, and
/// one with no date, are not requests the queue can carry.
/// </remarks>
public sealed record PrivacyRequestEntry(
    SubjectId Subject,
    PrivacyRequestType Type,
    string Detail,
    DateOnly ReceivedAt,
    string Channel,
    string IdentityConfirmation);
