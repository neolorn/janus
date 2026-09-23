using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One row of the generated records of processing: one purpose, with the columns the
/// regulator's template asks for in the order it asks for them.
/// </summary>
/// <param name="Purpose">What the processing is for.</param>
/// <param name="DataCategories">The categories of data it requires.</param>
/// <param name="SubjectCategories">The categories of person it is about.</param>
/// <param name="LawfulBasis">What it rests on.</param>
/// <param name="NonSensitive">Whether it is over data the deployment declares ordinary.</param>
/// <param name="Sensitive">Whether it is over data the deployment declares sensitive.</param>
/// <param name="Children">
/// Whether it is over a child's data, which is so exactly where a type it is declared
/// on declares the <c>children</c> sensitivity category (PRIV-SENS-001).
/// </param>
/// <param name="SensitiveCategories">Which sensitivity categories, where it is over any.</param>
/// <param name="Retention">
/// How long the data is kept, by category, longest first; empty where the deployment
/// has declared none, which is itself flagged.
/// </param>
/// <param name="Recipients">Who receives the data processed for it.</param>
/// <param name="DisposalMeasures">How the personal data is disposed of.</param>
/// <param name="RolesWithAccess">The roles holding a permission that serves it.</param>
/// <param name="TechnicalSecurityMeasures">The controls in force over it.</param>
/// <param name="Assessment">
/// The assessment its basis requires, where the basis requires one and one was
/// declared.
/// </param>
/// <remarks>Implements PRIV-ROPA-001, PRIV-SENS-001 and PRIV-RET-001.</remarks>
public sealed record ProcessingRecord(
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
