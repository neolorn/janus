using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Search;

/// <summary>
/// What the staff search reads of one subject from the database, decrypted: the name and
/// the contacts it searches, and the organizations whose search may answer it.
/// </summary>
/// <param name="Subject">The subject.</param>
/// <param name="Kind">Whether it is an account or a person record.</param>
/// <param name="DisplayName">Its display name, or nothing where it has none.</param>
/// <param name="Emails">
/// For an account each verified email, for a record its email, in canonical form.
/// </param>
/// <param name="Phones">
/// For an account each verified phone, for a record its phone, in E.164 form.
/// </param>
/// <param name="Organizations">
/// For an account the organizations it is a current member of; for a record the
/// organization that holds it.
/// </param>
/// <remarks>Implements IDN-SRCH-001 and IDN-SRCH-002 (D-256).</remarks>
internal sealed record SearchSubject(
    SubjectId Subject,
    SubjectKind Kind,
    [property: NeverLogged] string? DisplayName,
    [property: NeverLogged] IReadOnlyList<string> Emails,
    [property: NeverLogged] IReadOnlyList<string> Phones,
    IReadOnlyList<OrganizationId> Organizations);
