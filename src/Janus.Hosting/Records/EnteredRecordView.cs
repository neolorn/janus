using System;
using Janus.Core;

namespace Janus.Hosting.Records;

/// <summary>
/// The person record just entered, as the response carries it.
/// </summary>
/// <param name="Subject">Its subject identifier.</param>
/// <remarks>Implements chapter 09 section 8a and REC-COL-003.</remarks>
internal sealed record EnteredRecordView(Guid Subject)
{
    /// <summary>
    /// The view of one record's subject.
    /// </summary>
    /// <param name="subject">The subject.</param>
    /// <returns>The view.</returns>
    public static EnteredRecordView Of(SubjectId subject) => new(subject.Value);
}
