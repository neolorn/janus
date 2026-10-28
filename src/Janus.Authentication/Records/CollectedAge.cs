using System;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// The answer to the age question a collection session staged, as a record keeps it.
/// </summary>
/// <param name="AdultAffirmed">The affirmation derived, where the deployment takes one.</param>
/// <param name="Group">The age group, where the deployment takes no affirmation.</param>
/// <param name="DateOfBirth">The date given, where the deployment retains it.</param>
/// <param name="AnsweredAt">When the answer was given.</param>
/// <remarks>
/// Implements REC-AGE-001 and REC-COL-002.
/// </remarks>
internal sealed record CollectedAge(
    bool? AdultAffirmed,
    AgeGroup? Group,
    DateOnly? DateOfBirth,
    DateTimeOffset AnsweredAt);
