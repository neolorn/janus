using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Registration;

/// <summary>
/// Where a registration stands, which is what a step answers with and what the stream
/// carries.
/// </summary>
/// <param name="Step">The step the session is at.</param>
/// <param name="ExpiresAt">When the session stops accepting anything.</param>
/// <param name="Identifiers">What is staged, with its verification state.</param>
/// <param name="Security">What the security step has established.</param>
/// <remarks>Implements REG-SESS-002 and BFF-CSRF-005b.</remarks>
internal sealed record RegistrationStateView(
    RegistrationStep Step,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<StagedIdentifierView> Identifiers,
    RegistrationSecurityView Security)
{
    /// <summary>
    /// Reads the state of a session.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The state is absent.</exception>
    public static RegistrationStateView Of(RegistrationState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new RegistrationStateView(
            state.Step,
            state.ExpiresAt,
            [.. state.Identifiers.Select(StagedIdentifierView.Of)],
            RegistrationSecurityView.Of(state.Security));
    }
}
