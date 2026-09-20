using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Registration;

/// <summary>
/// Everything the one transaction of the terms step writes: the account, what it is
/// reached at, and what the person answered on the way in.
/// </summary>
/// <param name="Subject">The identifier the account takes.</param>
/// <param name="CreatedAt">The instant it comes into being.</param>
/// <param name="Identifiers">The verified identifiers it holds from the start.</param>
/// <param name="DateOfBirth">
/// The date the age screen took, present only where the deployment retains it.
/// </param>
/// <param name="AdultAffirmed">The affirmation derived at the age step.</param>
/// <param name="Group">The band recorded where the deployment takes no affirmation.</param>
/// <param name="AnsweredAgeAt">When the age screen was answered.</param>
/// <param name="TermsVersion">The version of the terms accepted.</param>
/// <param name="NoticeVersion">The version of the privacy notice presented.</param>
/// <param name="EmailMaximum">How many emails an account of this deployment holds.</param>
/// <param name="PhoneMaximum">How many phones an account of this deployment holds.</param>
/// <remarks>Implements REG-SESS-001, REG-SESS-007, REG-PROF-002 and REG-ACCT-001.</remarks>
internal sealed record NewAccount(
    SubjectId Subject,
    DateTimeOffset CreatedAt,
    IReadOnlyList<NewIdentifier> Identifiers,
    DateOnly? DateOfBirth,
    bool? AdultAffirmed,
    AgeGroup? Group,
    DateTimeOffset AnsweredAgeAt,
    string TermsVersion,
    string NoticeVersion,
    int EmailMaximum,
    int PhoneMaximum);
