using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Search;

/// <summary>
/// One subject as the index holds it: where each of its values lies in the index's
/// buffer, and what may see it.
/// </summary>
/// <param name="subject">The subject.</param>
/// <param name="kind">Whether it is an account or a person record.</param>
/// <param name="name">Where its normalised name lies, or nothing where it has none.</param>
/// <param name="words">Where each distinct word of its name lies.</param>
/// <param name="emails">Where each email lies.</param>
/// <param name="phones">Where each phone lies.</param>
/// <param name="organizations">The organizations whose search may answer it.</param>
/// <remarks>Implements IDN-SRCH-003 (D-256).</remarks>
internal sealed class IndexEntry(
    SubjectId subject,
    SubjectKind kind,
    TextRange? name,
    IReadOnlyList<TextRange> words,
    IReadOnlyList<TextRange> emails,
    IReadOnlyList<TextRange> phones,
    IReadOnlyList<OrganizationId> organizations)
{
    /// <summary>
    /// The subject.
    /// </summary>
    public SubjectId Subject { get; } = subject;

    /// <summary>
    /// Whether it is an account or a person record.
    /// </summary>
    public SubjectKind Kind { get; } = kind;

    /// <summary>
    /// Where its normalised name lies, or nothing where it has none.
    /// </summary>
    public TextRange? Name { get; } = name;

    /// <summary>
    /// Where each distinct word of its name lies, inside the name.
    /// </summary>
    public IReadOnlyList<TextRange> Words { get; } = words;

    /// <summary>
    /// Where each email lies.
    /// </summary>
    public IReadOnlyList<TextRange> Emails { get; } = emails;

    /// <summary>
    /// Where each phone lies.
    /// </summary>
    public IReadOnlyList<TextRange> Phones { get; } = phones;

    /// <summary>
    /// For an account, the organizations it is a current member of; for a record, the
    /// organization that holds it.
    /// </summary>
    public IReadOnlyList<OrganizationId> Organizations { get; } = organizations;

    /// <summary>
    /// Whether the subject has left the index, its values overwritten.
    /// </summary>
    public bool Removed { get; private set; }

    /// <summary>
    /// Marks the subject as having left the index.
    /// </summary>
    public void Remove() => Removed = true;
}
