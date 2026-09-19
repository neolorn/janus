using System.Collections.Generic;

namespace Janus.Authorization.Model;

/// <summary>
/// The built model as it is written to a file: every list in one order, so two runs
/// of one configuration produce the same bytes and a change to the model produces a
/// diff a reviewer reads.
/// </summary>
/// <param name="ResourceTypes">The kinds of thing the host holds, by name.</param>
/// <param name="Relationships">The facts a derivation may follow from, by name.</param>
/// <param name="Permissions">Every declared permission, the library's own included.</param>
/// <param name="LawfulBases">The bases a purpose may rest on, by key.</param>
/// <param name="SensitiveCategories">The categories a type may carry.</param>
/// <remarks>Implements AUTHZ-MODEL-005.</remarks>
internal sealed record SerializedModel(
    IReadOnlyList<SerializedModel.Type> ResourceTypes,
    IReadOnlyList<SerializedModel.Relationship> Relationships,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<SerializedModel.Basis> LawfulBases,
    IReadOnlyList<string> SensitiveCategories)
{
    /// <summary>
    /// One resource type.
    /// </summary>
    /// <param name="Name">The name the host chose.</param>
    /// <param name="ContainedIn">The type containing it, or nothing.</param>
    /// <param name="BelongsToOrganization">Whether it names the organization owning it.</param>
    /// <param name="Concealment">What a denial on one record discloses.</param>
    /// <param name="SensitiveCategories">The categories its data falls in.</param>
    /// <param name="Purposes">What it is processed for.</param>
    /// <param name="Derivations">The relationships that confer a role on it.</param>
    /// <param name="EncryptedFields">Its encrypted fields.</param>
    internal sealed record Type(
        string Name,
        string? ContainedIn,
        bool BelongsToOrganization,
        string Concealment,
        IReadOnlyList<string> SensitiveCategories,
        IReadOnlyList<Purpose> Purposes,
        IReadOnlyList<Derivation> Derivations,
        IReadOnlyList<Field> EncryptedFields);

    /// <summary>
    /// One purpose and the basis it rests on.
    /// </summary>
    /// <param name="Name">The purpose.</param>
    /// <param name="Basis">The basis it rests on.</param>
    /// <param name="Assessment">The assessment, where the basis requires one.</param>
    internal sealed record Purpose(string Name, string Basis, string? Assessment);

    /// <summary>
    /// One derivation.
    /// </summary>
    /// <param name="Relationship">The relationship it follows from.</param>
    /// <param name="Role">The role it confers.</param>
    /// <param name="Materialised">Whether it is precomputed into grant rows.</param>
    internal sealed record Derivation(string Relationship, string Role, bool Materialised);

    /// <summary>
    /// One encrypted field.
    /// </summary>
    /// <param name="Name">The field held as ciphertext.</param>
    /// <param name="SubjectColumn">The column naming the subject whose key encrypts it.</param>
    internal sealed record Field(string Name, string SubjectColumn);

    /// <summary>
    /// One relationship in the host's own data.
    /// </summary>
    /// <param name="Name">The relationship.</param>
    /// <param name="On">The resource type the fact is about.</param>
    /// <param name="Table">The host table holding it.</param>
    /// <param name="SubjectColumn">The column naming the subject.</param>
    /// <param name="Columns">Every column the evaluation reads.</param>
    internal sealed record Relationship(
        string Name,
        string On,
        string Table,
        string SubjectColumn,
        IReadOnlyList<string> Columns);

    /// <summary>
    /// One lawful basis and the properties the library branches on.
    /// </summary>
    /// <param name="Key">The basis.</param>
    /// <param name="IsConsent">Whether processing on it rests on consent.</param>
    /// <param name="RequiresWrittenConsentForSensitive">
    /// Whether sensitive data on it needs written consent.
    /// </param>
    /// <param name="RequiresAssessment">Whether a purpose on it carries an assessment.</param>
    /// <param name="IsObjectable">Whether a subject may object to processing on it.</param>
    internal sealed record Basis(
        string Key,
        bool IsConsent,
        bool RequiresWrittenConsentForSensitive,
        bool RequiresAssessment,
        bool IsObjectable);
}
