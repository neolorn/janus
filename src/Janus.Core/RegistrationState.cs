using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// A registration session as the screens see it. It is the whole of the state: a
/// wizard that kept its own copy would disagree with the server the moment a link
/// verified an identifier in another tab.
/// </summary>
/// <param name="Step">Where the registration has reached.</param>
/// <param name="ExpiresAt">When the session is swept, leaving nothing.</param>
/// <param name="Identifiers">Every identifier staged, with its state.</param>
/// <param name="Security">What the security step has established.</param>
/// <remarks>
/// Implements REG-SESS-001, REG-SESS-002 and chapter 9 section 2. It carries no
/// sign-in exit: a field present only for a duplicate would be an oracle.
/// </remarks>
public sealed record RegistrationState(
    RegistrationStep Step,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<StagedIdentifier> Identifiers,
    RegistrationSecurity Security);
