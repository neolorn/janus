using System.Collections.Generic;

namespace Janus.Hosting.Privacy;

/// <summary>
/// One row of the generated records of processing, in the template's field order.
/// </summary>
/// <param name="Purpose">What the processing is for.</param>
/// <param name="DataCategories">The categories of data it requires.</param>
/// <param name="SubjectCategories">The categories of person it is about.</param>
/// <param name="LawfulBasis">What it rests on.</param>
/// <param name="NonSensitive">Whether it is over ordinary data.</param>
/// <param name="Sensitive">Whether it is over sensitive data.</param>
/// <param name="Children">Whether it may be over a child's data.</param>
/// <param name="SensitiveCategories">Which sensitivity categories.</param>
/// <param name="Retention">How long the data is kept, by category, longest first.</param>
/// <param name="Recipients">Who receives the data.</param>
/// <param name="DisposalMeasures">How the personal data is disposed of.</param>
/// <param name="RolesWithAccess">The roles holding a permission that serves it.</param>
/// <param name="TechnicalSecurityMeasures">The controls in force over it.</param>
/// <param name="Assessment">The assessment its basis requires, where one was declared.</param>
/// <remarks>Implements PRIV-ROPA-001.</remarks>
internal sealed record ProcessingRecordView(
    string Purpose,
    IReadOnlyList<string> DataCategories,
    IReadOnlyList<string> SubjectCategories,
    string LawfulBasis,
    bool NonSensitive,
    bool Sensitive,
    bool Children,
    IReadOnlyList<string> SensitiveCategories,
    IReadOnlyList<string> Retention,
    IReadOnlyList<string> Recipients,
    IReadOnlyList<string> DisposalMeasures,
    IReadOnlyList<string> RolesWithAccess,
    IReadOnlyList<string> TechnicalSecurityMeasures,
    string? Assessment);
