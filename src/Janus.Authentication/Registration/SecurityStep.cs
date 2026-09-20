using System.Collections.Generic;
using Janus.Authentication.Factors;
using Janus.Core;

namespace Janus.Authentication.Registration;

/// <summary>
/// When the security step is done: the account has to reach at least one primary
/// sign-in method, and a password that does not stand alone has to be seconded.
/// </summary>
/// <remarks>
/// Implements REG-SESS-006, AUTH-PASS-001a and AUTH-RECOV-006. What counts is read
/// from the catalogue's properties and the policy's login factors (AUTH-FACT-001
/// AC1), so an entry added to the catalogue is a row and not an edit here: an entry
/// that rides an identifier becomes reachable the moment that identifier is proved.
/// </remarks>
internal static class SecurityStep
{
    /// <summary>
    /// Whether the step completes.
    /// </summary>
    /// <param name="session">The session as it stands.</param>
    /// <param name="permitted">The policy's login factors.</param>
    /// <returns>Whether the account would reach a way in.</returns>
    public static bool Complete(RegistrationSession session, IReadOnlySet<Factor> permitted) =>
        Primary(Reachable(session), permitted) && !SecondStepOutstanding(session, permitted);

    /// <summary>
    /// Whether a second step is mandatory and not yet enrolled, which is what a
    /// password below the single-factor floor leaves behind.
    /// </summary>
    /// <param name="session">The session as it stands.</param>
    /// <param name="permitted">The policy's login factors.</param>
    /// <returns>Whether one is still owed.</returns>
    public static bool SecondStepOutstanding(
        RegistrationSession session,
        IReadOnlySet<Factor> permitted) =>
        session.Password is not null
        && !session.PasswordStandsAlone
        && !Primary(WithoutThePassword(session), permitted)
        && !Seconded(session);

    /// <summary>
    /// Whether a second step stands beside a password, which is what recovery codes
    /// are drawn for.
    /// </summary>
    /// <param name="session">The session as it stands.</param>
    /// <returns>Whether the account would hold both.</returns>
    public static bool PasswordSeconded(RegistrationSession session) =>
        session.Password is not null && Seconded(session);

    // Every entry the account would be able to present: what the session stages, and
    // the entries that ride an identifier it has proved.
    private static List<Factor> Reachable(RegistrationSession session)
    {
        List<Factor> reachable = WithoutThePassword(session);

        if (session.Password is not null)
        {
            reachable.Add(FactorCatalogue.Password);
        }

        return reachable;
    }

    private static List<Factor> WithoutThePassword(RegistrationSession session)
    {
        var reachable = new List<Factor>(session.Credentials.Count);

        foreach (StagedCredential staged in session.Credentials)
        {
            reachable.Add(staged.Factor);
        }

        foreach (KeyValuePair<Factor, FactorProperties> entry in FactorCatalogue.Entries)
        {
            if (entry.Value.Channel is IdentifierKind channel && session.HasVerified(channel))
            {
                reachable.Add(entry.Key);
            }
        }

        return reachable;
    }

    private static bool Primary(List<Factor> reachable, IReadOnlySet<Factor> permitted)
    {
        foreach (Factor held in reachable)
        {
            if (permitted.Contains(held) && FactorCatalogue.Of(held).CanBePrimary)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Seconded(RegistrationSession session)
    {
        foreach (StagedCredential staged in session.Credentials)
        {
            if (SecondStep.Is(staged.Factor))
            {
                return true;
            }
        }

        return false;
    }
}
