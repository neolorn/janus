using System;
using Janus.Authentication.Factors;
using Janus.Core;

namespace Janus.Authentication.Registration;

/// <summary>
/// The email a social provider supplied with an identity, and whether the provider
/// operates the mailbox, which is what makes the sign-in its verification.
/// </summary>
/// <param name="Entered">The address as the provider wrote it.</param>
/// <param name="Operated">
/// Whether the provider operates the mailbox and said the address is verified.
/// </param>
/// <remarks>
/// Implements REG-IDENT-008 and D-146. Which mailboxes a provider operates is what its
/// catalogue entry carries, the provider-domain list the item states; any other
/// address a provider supplies is verified by one code like a typed address.
/// </remarks>
internal sealed record ProvidedAddress([property: NeverLogged] string Entered, bool Operated)
{
    /// <summary>
    /// Reads what an identity token said of the person's address.
    /// </summary>
    /// <param name="provider">Which provider issued the token.</param>
    /// <param name="email">The <c>email</c> claim, where there is one.</param>
    /// <param name="verified">Whether the <c>email_verified</c> claim is true.</param>
    /// <param name="hostedDomain">The <c>hd</c> claim, where there is one.</param>
    /// <returns>The address, or nothing where the token carries none this library stores.</returns>
    public static ProvidedAddress? Of(
        Factor provider,
        [NeverLogged] string? email,
        bool verified,
        string? hostedDomain)
    {
        if (email is null || !EmailAddress.TryParse(email, out EmailAddress address))
        {
            return null;
        }

        string domain = address.Value[(address.Value.LastIndexOf('@') + 1)..];
        FactorProperties entry = FactorCatalogue.Of(provider);

        bool operated = verified
            && (entry.OperatedDomains.Contains(domain)
                || (entry.OperatesHostedDomain
                    && hostedDomain is { Length: > 0 }
                    && string.Equals(domain, hostedDomain, StringComparison.OrdinalIgnoreCase)));

        return new ProvidedAddress(email, operated);
    }
}
