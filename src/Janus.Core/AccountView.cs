using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// Another person's account, as a host reads it: what it needs to deal with the person,
/// and never a credential, the date of birth or the photo.
/// </summary>
/// <param name="Subject">Whose account.</param>
/// <param name="State">Where the account stands.</param>
/// <param name="CreatedAt">When the account was created.</param>
/// <param name="Profile">The names the person gave.</param>
/// <param name="Identifiers">What the account is known by.</param>
/// <param name="Preferences">The language, the time zone and the declared values.</param>
/// <param name="Memberships">The organizations the account is a current member of.</param>
/// <remarks>Implements IDN-READ-003.</remarks>
public sealed record AccountView(
    SubjectId Subject,
    AccountState State,
    DateTimeOffset CreatedAt,
    AccountViewProfile Profile,
    AccountViewIdentifiers Identifiers,
    PreferenceValues Preferences,
    IReadOnlyList<OrganizationId> Memberships);
