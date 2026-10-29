using System;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// The answer to the age question a person record holds, as its read shows it: never the
/// date of birth.
/// </summary>
/// <param name="AdultAffirmed">The affirmation, where the answer is one.</param>
/// <param name="Group">The age group, where the answer is one.</param>
/// <param name="AnsweredAt">When the person answered.</param>
/// <remarks>Implements REC-ACC-002, REC-AGE-001, chapter 09 section 7a.</remarks>
internal sealed record RecordAge(bool? AdultAffirmed, AgeGroup? Group, DateTimeOffset AnsweredAt);
