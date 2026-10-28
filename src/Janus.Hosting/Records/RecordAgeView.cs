using System;
using Janus.Authentication.Records;
using Janus.Core;

namespace Janus.Hosting.Records;

/// <summary>
/// A person record's age answer, as its read shows it: never the date of birth.
/// </summary>
/// <param name="AnsweredAt">When the answer was given.</param>
/// <param name="Affirmed">Whether the person affirmed being an adult, where the answer holds it.</param>
/// <param name="AgeGroup">The age group, where the answer holds it.</param>
/// <remarks>Implements REC-ACC-002, chapter 09 section 7a.</remarks>
internal sealed record RecordAgeView(DateTimeOffset AnsweredAt, bool? Affirmed, AgeGroup? AgeGroup)
{
    /// <summary>
    /// Shows a record's age answer.
    /// </summary>
    /// <param name="age">The answer.</param>
    /// <returns>Its view.</returns>
    /// <exception cref="ArgumentNullException">The answer is absent.</exception>
    public static RecordAgeView Of(RecordAge age)
    {
        ArgumentNullException.ThrowIfNull(age);

        return new RecordAgeView(age.AnsweredAt, age.AdultAffirmed, age.Group);
    }
}
