using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Passwords;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// The second step of a password sign-in: which entries are one, and which of them an
/// account may enrol.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-002b. A second step is second to a password, so an account
/// holding none has no second step to offer; a set of recovery codes is not one,
/// because it is what the account falls back to rather than what it signs in with.
/// </remarks>
internal static class SecondStep
{
    /// <summary>
    /// Whether an entry is a second step rather than a way in of its own or a set of
    /// single-use codes.
    /// </summary>
    /// <param name="factor">The entry.</param>
    /// <returns>Whether it is one.</returns>
    public static bool Is(Factor factor) => FactorCatalogue.Of(factor) is
    {
        CanBeSecondFactor: true,
        CanBePrimary: false,
        SingleUse: false,
    };

    /// <summary>
    /// Whether an account holds what a second step is second to.
    /// </summary>
    /// <param name="passwords">Where the account's password is read.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it holds a password.</returns>
    public static async ValueTask<bool> AvailableAsync(
        IPasswordStore passwords,
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await passwords.FindAsync(subject, cancellationToken).ConfigureAwait(false) is not null;

    /// <summary>
    /// The second-step methods to offer an account, which is none until it holds a
    /// password.
    /// </summary>
    /// <param name="password">Whether the account holds one.</param>
    /// <param name="permitted">The policy's login factors.</param>
    /// <returns>What the account may enrol.</returns>
    public static IReadOnlyList<Factor> Offerable(bool password, IReadOnlySet<Factor> permitted) =>
        password ? [.. permitted.Where(Is).Order()] : [];

    /// <summary>
    /// Which of an account's second steps is offered first: the one the person
    /// marked, and where none is marked the one enrolled most recently
    /// (IDN-ATTR-008).
    /// </summary>
    /// <param name="enrolled">Every credential the account holds.</param>
    /// <returns>The one to offer first, and nothing where it holds no second step.</returns>
    /// <remarks>
    /// The answer is derived rather than held, so removing the marked one moves the
    /// preference on its own and an enrolment on an unmarked account becomes the
    /// preference without a second write.
    /// </remarks>
    public static Authenticator? Preferred(IEnumerable<Authenticator> enrolled)
    {
        Authenticator[] usable = [.. enrolled.Where(Usable)];

        return Array.Find(usable, credential => credential.IsPreferred)
            ?? usable.OrderByDescending(credential => credential.AddedAt).FirstOrDefault();
    }

    private static bool Usable(Authenticator credential) =>
        credential.State is AuthenticatorState.Active && Is(credential.Factor);
}
