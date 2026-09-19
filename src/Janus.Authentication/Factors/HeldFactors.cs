using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// What an account holds, as a gate reads it: the factors it can present now, the
/// factors still standing against it, and when a loss report against one of them
/// completes.
/// </summary>
/// <param name="Usable">
/// The catalogue entries the account can present at this instant: enrolled,
/// confirmed and in state <see cref="AuthenticatorState.Active"/>.
/// </param>
/// <param name="Standing">
/// The catalogue entries not yet invalidated, which is what reachable assurance
/// counts: a reported loss lowers nothing until its window completes
/// (AUTH-STEP-008 invariant 5).
/// </param>
/// <param name="LossCompletes">
/// When the pending loss report against one of them completes, and nothing where
/// none is pending.
/// </param>
/// <remarks>Implements AUTH-STEP-002, AUTH-STEP-006 and AUTH-RECOV-007.</remarks>
internal sealed record HeldFactors(
    IReadOnlySet<Factor> Usable,
    IReadOnlySet<Factor> Standing,
    DateTimeOffset? LossCompletes)
{
    /// <summary>
    /// What the account's credentials come to.
    /// </summary>
    /// <param name="authenticators">Every credential of the account, in any state.</param>
    /// <param name="password">Whether the account holds a password.</param>
    /// <returns>The factors as a gate reads them.</returns>
    /// <exception cref="ArgumentNullException">The credentials are absent.</exception>
    public static HeldFactors Of(IReadOnlyCollection<Authenticator> authenticators, bool password)
    {
        ArgumentNullException.ThrowIfNull(authenticators);

        HashSet<Factor> usable = [.. authenticators
            .Where(credential => credential.IsUsable)
            .Select(credential => credential.Factor)];

        HashSet<Factor> standing = [.. authenticators
            .Where(credential =>
                credential.Confirmed && credential.State is not AuthenticatorState.Invalidated)
            .Select(credential => credential.Factor)];

        if (password)
        {
            usable.Add(Factor.Password);
            standing.Add(Factor.Password);
        }

        return new HeldFactors(
            usable,
            standing,
            authenticators
                .Where(credential => credential.State is AuthenticatorState.Suspended)
                .Min(credential => credential.InvalidatesAt));
    }
}
