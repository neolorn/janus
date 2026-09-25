using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Janus.Core;

namespace Janus.Storage.Authentication.Registration;

/// <summary>
/// Everything a registration session staged, as it is written into its one encrypted
/// column.
/// </summary>
/// <param name="Client">The originating application.</param>
/// <param name="Language">The language the messages go out in.</param>
/// <param name="Source">The address the registration was started from.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="Step">Where it has reached.</param>
/// <param name="DateOfBirth">The date retained, where the deployment retains it.</param>
/// <param name="AdultAffirmed">The affirmation derived at the age screen.</param>
/// <param name="Group">The band recorded instead, where one was.</param>
/// <param name="AnsweredAgeAt">When the age screen was answered.</param>
/// <param name="AgeRefused">Whether an under-age date ended it.</param>
/// <param name="PhoneSkipped">Whether the phone step was passed over.</param>
/// <param name="Password">The password hash staged, in its encoded form.</param>
/// <param name="PasswordStandsAlone">Whether it reaches the single-factor floor.</param>
/// <param name="RecoveryCodes">The set drawn at the security step, encoded.</param>
/// <param name="Identifiers">Every identifier staged.</param>
/// <param name="Credentials">Every credential enrolled against it.</param>
/// <param name="Invitation">The invitation whose link opened it, where one did.</param>
/// <remarks>
/// Implements REG-SESS-001 and REG-SESS-002. The shape is generated at build time
/// rather than reflected over at run time, so the column's format is fixed by
/// something a reader can see.
/// </remarks>
internal sealed record StagedSessionDocument(
    string Client,
    string Language,
    string Source,
    DateTimeOffset CreatedAt,
    string Step,
    DateOnly? DateOfBirth,
    bool? AdultAffirmed,
    string? Group,
    DateTimeOffset? AnsweredAgeAt,
    bool AgeRefused,
    bool PhoneSkipped,
    [property: NeverLogged] string? Password,
    bool PasswordStandsAlone,
    IReadOnlyList<string>? RecoveryCodes,
    IReadOnlyList<StagedIdentityDocument> Identifiers,
    IReadOnlyList<StagedCredentialDocument> Credentials,
    Guid? Invitation);
