using System.Collections.Generic;

namespace Janus.Hosting.Registration;

/// <summary>
/// The terms step, which is where the account comes into existence.
/// </summary>
/// <param name="TermsVersion">The version of the terms presented.</param>
/// <param name="NoticeVersion">The version of the privacy notice presented.</param>
/// <param name="Consents">What each consent control was left at, by purpose.</param>
/// <remarks>Implements REG-SESS-007.</remarks>
internal sealed record TermsRequest(
    string? TermsVersion,
    string? NoticeVersion,
    IReadOnlyDictionary<string, bool>? Consents);
