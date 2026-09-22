namespace Janus.Hosting.Privacy;

/// <summary>
/// What an authorised human enters for a request that arrived out of band.
/// </summary>
/// <param name="Subject">Whose request it is.</param>
/// <param name="Type">What is asked for.</param>
/// <param name="Detail">What the request said.</param>
/// <param name="ReceivedAt">
/// The calendar date it reached the company, <c>YYYY-MM-DD</c>, never later than
/// today in the deployment zone.
/// </param>
/// <param name="Channel">How it arrived.</param>
/// <param name="IdentityConfirmation">What confirmed the requester is the subject.</param>
/// <remarks>Implements chapter 09 section 8a, PRIV-RIGHT-001, D-113 and D-136.</remarks>
internal sealed record PrivacyEntryBody(
    string? Subject,
    string? Type,
    string? Detail,
    string? ReceivedAt,
    string? Channel,
    string? IdentityConfirmation);
