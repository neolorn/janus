using System;
using Janus.Core;

namespace Janus.Identity.Records;

/// <summary>
/// The answer a person record holds to the age question: the affirmation where the
/// deployment requires one, otherwise the age group, with the instant it was given.
/// </summary>
/// <param name="AdultAffirmed">The affirmation, where the deployment takes one.</param>
/// <param name="Group">The age group, where it takes none.</param>
/// <param name="AnsweredAt">When the person answered.</param>
/// <param name="DateOfBirth">The date entered, where the deployment retains it.</param>
/// <remarks>
/// Implements REC-AGE-001 and REG-PROF-002. An answer holds one of the two forms and
/// never both, as an account's does.
/// </remarks>
internal sealed record RecordAgeAnswer(
    bool? AdultAffirmed,
    AgeGroup? Group,
    DateTimeOffset AnsweredAt,
    DateOnly? DateOfBirth)
{
    /// <summary>
    /// Whether the answer holds exactly one of its two forms.
    /// </summary>
    public bool IsWellFormed => AdultAffirmed is null != Group is null;
}
