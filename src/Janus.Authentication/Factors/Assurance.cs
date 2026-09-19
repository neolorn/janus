using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// What an authentication reached: the tier, and whether what reached it resists
/// credential relay.
/// </summary>
/// <param name="Level">The tier.</param>
/// <param name="PhishingResistant">Whether a factor that resists relay was among them.</param>
/// <remarks>
/// Implements AUTH-SESS-005a. The one arithmetic: it assigns what a sign-in records,
/// what a combination presented at a gate contributes, and what an account can reach
/// with the factors it holds.
/// </remarks>
internal readonly record struct Assurance(AssuranceLevel Level, bool PhishingResistant)
{
    /// <summary>
    /// What the factors presented together reach.
    /// </summary>
    /// <param name="presented">The properties of the factors presented.</param>
    /// <returns>
    /// What they reach, or nothing where none of them may begin an authentication: a
    /// second factor alone contributes nothing.
    /// </returns>
    /// <exception cref="ArgumentNullException">The collection is absent.</exception>
    public static Assurance? Reached(IReadOnlyCollection<FactorProperties> presented)
    {
        ArgumentNullException.ThrowIfNull(presented);

        // A factor that proves control of a channel never authenticates, whatever else
        // it is registered with (AUTH-FACT-001).
        List<FactorProperties> counting = [.. presented.Where(factor => !factor.VerificationOnly)];
        List<FactorProperties> primaries = [.. counting.Where(factor => factor.CanBePrimary)];

        if (primaries.Count == 0)
        {
            return null;
        }

        // Signed in on another party's word: a second factor contributes nothing
        // without a first factor of ours beside it, and we assert no tier of our own.
        return primaries.TrueForAll(factor => factor.AssuranceLevel is AssuranceLevel.Delegated)
            ? new Assurance(AssuranceLevel.Delegated, PhishingResistant: false)
            : new Assurance(
                counting.Max(factor => factor.AssuranceLevel),
                counting.Exists(factor => factor.IsPhishingResistant));
    }

    /// <summary>
    /// What the factors presented together prove on a session that already exists,
    /// which is the same arithmetic over the factors that count after a sign-in.
    /// </summary>
    /// <param name="presented">The properties of the factors presented.</param>
    /// <returns>
    /// What they prove, or nothing where none of them counts here: an email factor,
    /// a link and a second factor alone each contribute nothing.
    /// </returns>
    /// <exception cref="ArgumentNullException">The collection is absent.</exception>
    public static Assurance? Proved(IReadOnlyCollection<FactorProperties> presented)
    {
        ArgumentNullException.ThrowIfNull(presented);

        return Reached([.. presented.Where(factor => !factor.SignInOnly)]);
    }
}
