using System;
using Janus.Core;

namespace Janus.Identity.Accounts;

/// <summary>
/// What the registration that created an account recorded: the answer to the age
/// screen, and the documents the person accepted and was shown.
/// </summary>
/// <param name="AdultAffirmed">
/// Whether the date entered made the person an adult, and nothing where the
/// deployment takes no affirmation.
/// </param>
/// <param name="Group">
/// The band recorded instead, where the deployment takes no affirmation.
/// </param>
/// <param name="AnsweredAgeAt">When the age screen was answered.</param>
/// <param name="TermsVersion">
/// The version of the terms accepted, and nothing where no terms step ran.
/// </param>
/// <param name="NoticeVersion">
/// The version of the privacy notice presented, and nothing where no terms step ran.
/// </param>
/// <remarks>
/// Implements REG-PROF-002, REG-SESS-007, PRIV-CONS-008a and PRIV-MINOR-001. The date
/// itself is a profile attribute and is retained only where <c>profile.dateofbirth</c>
/// is not off; what is here is the derived answer, which every account a person
/// answered for carries. The first administrator answers the age screen at bootstrap,
/// where no terms step runs, so that account names no document.
/// </remarks>
internal sealed record AccountRegistration(
    bool? AdultAffirmed,
    AgeGroup? Group,
    DateTimeOffset AnsweredAgeAt,
    string? TermsVersion,
    string? NoticeVersion);
