using System;

namespace Janus.Core;

/// <summary>
/// The age answer a person record holds, never the date of birth it was computed from.
/// </summary>
/// <param name="AdultAffirmed">The affirmation, where the host requires one.</param>
/// <param name="Group">The age group, where the host serves minors.</param>
/// <param name="AnsweredAt">When the person answered.</param>
/// <remarks>Implements REC-ADM-001 and REC-AGE-001.</remarks>
public sealed record PersonRecordAge(bool? AdultAffirmed, AgeGroup? Group, DateTimeOffset AnsweredAt);
