using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Search;

/// <summary>
/// What the index holds of one subject, each value already in the form it is searched
/// in: the name normalised, each verified email in its canonical form and each verified
/// phone as the digits of its E.164 form.
/// </summary>
/// <param name="Subject">The subject.</param>
/// <param name="Kind">Whether it is an account or a person record.</param>
/// <param name="Name">The normalised name, or nothing where it has none.</param>
/// <param name="Emails">The emails searched.</param>
/// <param name="Phones">The phones searched.</param>
/// <param name="Organizations">
/// For an account, the organizations it is a current member of; for a record, the
/// organization that holds it.
/// </param>
/// <remarks>Implements IDN-SRCH-001 and IDN-SRCH-002 (D-256).</remarks>
internal sealed record IndexedSubject(
    SubjectId Subject,
    SubjectKind Kind,
    string? Name,
    IReadOnlyList<string> Emails,
    IReadOnlyList<string> Phones,
    IReadOnlyList<OrganizationId> Organizations);
