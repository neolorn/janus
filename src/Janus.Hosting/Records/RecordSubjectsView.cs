using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Records;

/// <summary>
/// The subjects of some person records, as the response carries them.
/// </summary>
/// <param name="Subjects">Their subject identifiers.</param>
/// <remarks>Implements chapter 09 section 8a and REC-ADM-001.</remarks>
internal sealed record RecordSubjectsView(IReadOnlyList<Guid> Subjects)
{
    /// <summary>
    /// The view of some records' subjects.
    /// </summary>
    /// <param name="subjects">The subjects.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The subjects are absent.</exception>
    public static RecordSubjectsView Of(IReadOnlyList<SubjectId> subjects)
    {
        ArgumentNullException.ThrowIfNull(subjects);

        return new RecordSubjectsView([.. subjects.Select(subject => subject.Value)]);
    }
}
