using System;

namespace Janus.Core;

/// <summary>
/// The profile group of an account.
/// </summary>
/// <param name="DisplayName">
/// The name shown where a human needs to know who the account is.
/// </param>
/// <param name="LegalName">
/// The legal name, a proofing attribute absent while <c>profile.legalname</c> is
/// off.
/// </param>
/// <param name="DateOfBirth">
/// The date entered at the age step, absent while <c>profile.dateofbirth</c> is off.
/// </param>
/// <param name="PhotoUpdatedAt">
/// When the photo was last set, absent where the account shows none. The image itself
/// is read from the photo endpoint and never from here.
/// </param>
/// <remarks>Implements REG-ACCT-001, REG-PROF-001 and IDN-ATTR-002.</remarks>
public sealed record ProfileDetail(
    string? DisplayName,
    string? LegalName,
    DateOnly? DateOfBirth,
    DateTimeOffset? PhotoUpdatedAt);
