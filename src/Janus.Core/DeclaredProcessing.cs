using System;
using System.Collections.Generic;
using System.Linq;

namespace Janus.Core;

/// <summary>
/// What a deployment processes, read as the person exercising a right sees it: one
/// entry per purpose, gathered from every resource type declaring it.
/// </summary>
/// <remarks>
/// Implements PRIV-BASIS-003, PRIV-SENS-002, PRIV-CONS-002, PRIV-CONS-011,
/// PRIV-RIGHT-001a and PRIV-ROPA-001. The consent a purpose requires is derived here
/// and nowhere else, so declaring a type sensitive changes what its consent-based
/// purposes ask for and nothing else has to be edited (AUTHZ-MODEL-003 AC2).
/// </remarks>
public sealed class DeclaredProcessing
{
    private readonly Dictionary<string, DeclaredPurpose> _purposes;

    private DeclaredProcessing(Dictionary<string, DeclaredPurpose> purposes) => _purposes = purposes;

    /// <summary>
    /// Every purpose the deployment declares, in the order a reader expects.
    /// </summary>
    public IReadOnlyList<DeclaredPurpose> Purposes =>
        [.. _purposes.Values.OrderBy(purpose => purpose.Name, StringComparer.Ordinal)];

    /// <summary>
    /// Reads the purposes out of what the host declared.
    /// </summary>
    /// <param name="declaration">What the host declared.</param>
    /// <returns>The processing.</returns>
    /// <exception cref="ArgumentNullException">The declaration is absent.</exception>
    /// <exception cref="StartupException">
    /// A purpose rests on an undeclared basis, is declared on two bases, or declares
    /// the ordinary path where its basis requires the written one over sensitive data.
    /// </exception>
    public static DeclaredProcessing Of(AuthorizationDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        var bases = declaration.LawfulBases.ToDictionary(basis => basis.Key, StringComparer.Ordinal);
        var purposes = new Dictionary<string, DeclaredPurpose>(StringComparer.Ordinal);

        foreach (ResourceTypeDeclaration type in declaration.ResourceTypes)
        {
            foreach (PurposeDeclaration declared in type.Purposes)
            {
                if (!bases.TryGetValue(declared.Basis, out LawfulBasisDeclaration? basis))
                {
                    continue;
                }

                purposes[declared.Name] = Gathered(
                    type,
                    declared,
                    basis,
                    purposes.GetValueOrDefault(declared.Name));
            }
        }

        return new DeclaredProcessing(purposes);
    }

    /// <summary>
    /// One purpose by name, or nothing where the deployment declares none.
    /// </summary>
    /// <param name="purpose">The purpose.</param>
    /// <returns>It, or nothing.</returns>
    public DeclaredPurpose? Find(string purpose) =>
        _purposes.TryGetValue(purpose ?? string.Empty, out DeclaredPurpose? found) ? found : null;

    private static DeclaredPurpose Gathered(
        ResourceTypeDeclaration type,
        PurposeDeclaration declared,
        LawfulBasisDeclaration basis,
        DeclaredPurpose? already)
    {
        if (already is not null && already.Basis.Key != basis.Key)
        {
            throw new StartupException(
                "The purpose " + declared.Name + " is declared on two lawful bases, "
                + already.Basis.Key + " and " + basis.Key + ".");
        }

        List<string> sensitive = [.. already?.SensitiveCategories ?? [], .. type.SensitiveCategories];

        return new DeclaredPurpose(
            declared.Name,
            basis,
            Consent(declared, basis, sensitive),
            declared.Assessment ?? already?.Assessment,
            [.. Distinct([.. already?.DataCategories ?? [], .. declared.DataCategories])],
            [.. Distinct([.. already?.SubjectCategories ?? [], .. declared.SubjectCategories])],
            [.. Distinct(sensitive)],
            [.. already?.Types ?? [], type.Name],
            Document(declared, already));
    }

    // PRIV-CONS-007: the document a consent for the purpose is recorded against is
    // one document, so two types declaring the same purpose against two documents,
    // or one against a document and one against the notice, is a deployment that
    // cannot say which revision ends the consent.
    private static string? Document(PurposeDeclaration declared, DeclaredPurpose? already)
    {
        if (already is not null
            && !string.Equals(already.Document, declared.Document, StringComparison.Ordinal))
        {
            throw new StartupException(
                "The purpose " + declared.Name
                + " names two governing documents, "
                + (already.Document ?? "the privacy notice") + " and "
                + (declared.Document ?? "the privacy notice") + ".");
        }

        return declared.Document;
    }

    // The written path where sensitive data rests on a basis that requires it, the
    // ordinary path otherwise; a declaration may ask for more and never for less.
    private static ConsentKind? Consent(
        PurposeDeclaration declared,
        LawfulBasisDeclaration basis,
        List<string> sensitive)
    {
        if (!basis.IsConsent)
        {
            return null;
        }

        ConsentKind derived = basis.RequiresWrittenConsentForSensitive && sensitive.Count > 0
            ? ConsentKind.Written
            : ConsentKind.Ordinary;

        if (derived is ConsentKind.Written && declared.Consent is ConsentKind.Ordinary)
        {
            throw new StartupException(
                "The purpose " + declared.Name
                + " is declared on the ordinary path over sensitive data, and its basis "
                + basis.Key + " requires the written one.");
        }

        return declared.Consent is ConsentKind.Written ? ConsentKind.Written : derived;
    }

    private static IEnumerable<string> Distinct(IReadOnlyList<string> values) =>
        values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
}
