using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One purpose as the deployment declares it, gathered across every resource type
/// that names it: what it rests on, what it requires, and what it is over.
/// </summary>
/// <param name="Name">The purpose.</param>
/// <param name="Basis">The lawful basis it rests on, with the properties the code reads.</param>
/// <param name="Consent">
/// Which capture path a consent for it runs through, or nothing where its basis is
/// not consent.
/// </param>
/// <param name="Assessment">The assessment reference, where its basis requires one.</param>
/// <param name="DataCategories">The categories of data it requires.</param>
/// <param name="SubjectCategories">The categories of person it is about.</param>
/// <param name="SensitiveCategories">
/// The sensitivity categories of the types it is declared on, which is empty where it
/// is over no sensitive type.
/// </param>
/// <param name="Types">The resource types declaring it.</param>
/// <remarks>
/// Implements PRIV-BASIS-001, PRIV-BASIS-003, PRIV-SENS-002, PRIV-CONS-002,
/// PRIV-RIGHT-001a and PRIV-ROPA-001. A purpose is one thing to the person exercising
/// a right over it, whichever of the host's types carry it.
/// </remarks>
public sealed record DeclaredPurpose(
    string Name,
    LawfulBasisDeclaration Basis,
    ConsentKind? Consent,
    string? Assessment,
    IReadOnlyList<string> DataCategories,
    IReadOnlyList<string> SubjectCategories,
    IReadOnlyList<string> SensitiveCategories,
    IReadOnlyList<ResourceType> Types);
