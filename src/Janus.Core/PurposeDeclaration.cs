using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One purpose a resource type is processed for, the lawful basis it rests on, and
/// what it requires: the categories of data and the categories of person the records
/// of processing are generated from.
/// </summary>
/// <param name="Name">The purpose, as the records of processing name it.</param>
/// <param name="Basis">The key of the declared lawful basis it rests on.</param>
/// <param name="Assessment">
/// The reference to the legitimate interest assessment, where the basis requires one.
/// </param>
/// <param name="DataCategories">
/// The specific categories of data the purpose requires, which is what makes the
/// declaration a statement of what is collected rather than of what happens to be
/// held.
/// </param>
/// <param name="SubjectCategories">The categories of person the purpose is about.</param>
/// <param name="Consent">
/// The capture path a consent for it runs through, where the deployment states one.
/// Absent, the path follows from the basis and the sensitivity of the type, and a
/// declaration may ask for the written path and never for less.
/// </param>
/// <param name="Document">
/// The legal document whose version a consent for it is recorded against, and a
/// material revision of which ends that consent. Absent, the privacy notice governs
/// it (PRIV-CONS-001, PRIV-CONS-007).
/// </param>
/// <remarks>
/// Implements AUTHZ-MODEL-003, PRIV-PRIN-001, PRIV-BASIS-001, PRIV-BASIS-002,
/// PRIV-BASIS-003, PRIV-CONS-007, PRIV-ROPA-001.
/// </remarks>
public sealed record PurposeDeclaration(
    string Name,
    string Basis,
    string? Assessment,
    IReadOnlyList<string> DataCategories,
    IReadOnlyList<string> SubjectCategories,
    ConsentKind? Consent,
    string? Document);
