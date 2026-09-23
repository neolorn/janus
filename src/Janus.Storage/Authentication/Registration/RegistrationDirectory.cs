using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Identity.Identifiers;
using Janus.Identity.Profiles;
using Janus.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Registration;

/// <summary>
/// What registration asks of the account directory, over the identity stores.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="accounts">Where the account row is written.</param>
/// <param name="identifiers">Where the account's identifiers are read and written.</param>
/// <param name="profiles">Where the date of birth is written.</param>
/// <param name="subjectKeys">Where the account's data key is drawn and written.</param>
/// <remarks>
/// Implements REG-SESS-001, REG-SESS-005 and REG-SESS-007. The account, its key, its
/// identifiers and its profile are written inside the caller's transaction, so the
/// whole of it commits or none of it does.
/// </remarks>
internal sealed class RegistrationDirectory(
    StoreContext context,
    IAccountStore accounts,
    IIdentifierStore identifiers,
    IProfileStore profiles,
    ISubjectKeyStore subjectKeys) : IRegistrationDirectory
{
    /// <inheritdoc/>
    public ValueTask<SubjectId?> OwnerAsync(
        IdentifierKind kind,
        string canonical,
        CancellationToken cancellationToken) =>
        identifiers.FindOwnerAsync(kind, canonical, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask CreateAsync(NewAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        await accounts
            .AddAsync(
                Account.Create(
                    account.Subject,
                    account.CreatedAt,
                    new AccountRegistration(
                        account.AdultAffirmed,
                        account.Group,
                        account.AnsweredAgeAt,
                        account.TermsVersion,
                        account.NoticeVersion)),
                cancellationToken)
            .ConfigureAwait(false);

        await subjectKeys.CreateAsync(account.Subject, cancellationToken).ConfigureAwait(false);

        // The rows the account's own stores write are read back through them, so the
        // account exists before anything names it.
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await IdentifiersAsync(account, cancellationToken).ConfigureAwait(false);

        if (account.DateOfBirth is DateOnly born)
        {
            var profile = Profile.Empty(account.Subject);
            profile.RecordDateOfBirth(born);

            await profiles.RecordAsync(profile, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask IdentifiersAsync(NewAccount account, CancellationToken cancellationToken)
    {
        IdentifierSet set = await identifiers
            .FindBySubjectAsync(account.Subject, cancellationToken)
            .ConfigureAwait(false);

        foreach (NewIdentifier staged in account.Identifiers)
        {
            set.Add(
                Taken(account.Subject, staged, account.CreatedAt),
                staged.Kind is IdentifierKind.Email
                    ? account.EmailMaximum
                    : account.PhoneMaximum);

            set.Verify(staged.Id, staged.VerifiedAt);
        }

        // REG-IDENT-002: the first of a kind is that kind's primary, which the set
        // settles as each is added, so nothing here names one.
        await identifiers.RecordAsync(set, cancellationToken).ConfigureAwait(false);
    }

    // A value the registration staged is canonical already, so a form that no longer
    // parses is a corrupted session and not a value to take on quietly.
    private static Identifier Taken(SubjectId subject, NewIdentifier staged, DateTimeOffset at) =>
        staged.Kind is IdentifierKind.Email
            ? Identifier.Email(staged.Id, subject, Address(staged.Canonical), staged.Entered, at, staged.Locked)
            : Identifier.Phone(staged.Id, subject, Number(staged.Canonical), staged.Entered, at);

    private static EmailAddress Address(string canonical) =>
        EmailAddress.TryParse(canonical, out EmailAddress address)
            ? address
            : throw new InvalidOperationException("The staged address is not an address.");

    private static PhoneNumber Number(string canonical) =>
        PhoneNumber.TryParse(canonical, out PhoneNumber number)
            ? number
            : throw new InvalidOperationException("The staged number is not a number.");
}
