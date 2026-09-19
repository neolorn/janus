using Janus.Core;

namespace Janus.Authentication.Policies;

/// <summary>
/// What a raised requirement holds over a sign-in: what the account has to meet, by
/// when, and whether the run-up has already ended.
/// </summary>
/// <param name="Requirement">The requirement and its deadline.</param>
/// <param name="Expired">
/// Whether the run-up has ended, after which the sign-in stops at enrolment rather
/// than carrying the requirement and continuing.
/// </param>
/// <remarks>Implements AUTH-FACT-017 and AUTH-SESS-009.</remarks>
internal sealed record PolicyHold(PolicyRequirement Requirement, bool Expired);
