using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Mailboxes;
using Janus.Authentication.Organizations;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Invitations;

/// <summary>
/// The end of a membership by an administrator: the membership ends, and where it gave
/// the account a corporate address the address and its mailbox are retired and the
/// personal email becomes the primary.
/// </summary>
/// <param name="gate">The one place a permission is evaluated.</param>
/// <param name="directory">Where the organization's standing is read.</param>
/// <param name="memberships">Where the membership is ended.</param>
/// <param name="identifiers">Where the account's identifiers are read and the corporate address retired.</param>
/// <param name="mailboxes">Where the mailbox the account holds is retired.</param>
/// <param name="sending">What tells the security-notice set of the new primary.</param>
/// <param name="events">Where the end and the new primary are announced.</param>
/// <param name="configuration">Where the languages are read.</param>
/// <param name="audit">Where the end is written down.</param>
/// <param name="work">The one transaction the end runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements REG-MAIL-003, IDN-MEM-001, INT-MAIL-006a and chapter 09 section 8a. The
/// account's state and its grants are left as they are: suspension and the removal of
/// grants are separate steps of offboarding. Everything is written in one transaction,
/// so no instant exists at which the account holds no primary email.
/// </remarks>
internal sealed class MembershipEnd(
    IAccessGate gate,
    IOrganizationDirectory directory,
    IMembershipEnding memberships,
    IIdentifierDirectory identifiers,
    IMailboxStore mailboxes,
    INotificationHandler sending,
    IEvents events,
    IConfigurationStore configuration,
    IOrganizationAudit audit,
    IUnitOfWork work,
    TimeProvider time)
{
    private const string Ended = "membership-ended";

    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(capacity: 0, StringComparer.Ordinal);

    /// <summary>
    /// Ends an account's membership of an organization.
    /// </summary>
    /// <param name="context">Who is ending it.</param>
    /// <param name="organization">Of which organization.</param>
    /// <param name="member">Whose membership.</param>
    /// <param name="source">The address the request came from, which a notice counts against.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal of <see cref="IInvitations.EndMembershipAsync"/>.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result> EndAsync(
        AccessContext context,
        OrganizationId organization,
        SubjectId member,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);

        // A change is made by a person, whose identity the record carries.
        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if ((await gate
                    .RequireAsync(context, Permissions.MembershipManage, organization, cancellationToken)
                    .ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error refused)
        {
            return Result.Failure(refused);
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (await memberships.EndAsync(member, organization, now, cancellationToken).ConfigureAwait(false)
            is not MembershipId ended)
        {
            return Result.Failure(Error.From(
                ErrorCodes.RequestMalformed,
                "member",
                JsonSerializer.SerializeToElement("subject")));
        }

        List<DomainEvent> announced =
        [
            new MembershipChanged(now, Key(ended, now), ended, organization, MembershipChange.Ended)
            {
                Subject = member,
            },
        ];

        // The mailboxes are the administrative organization's, so only the end of that
        // membership takes one back; another the account ends leaves it in place.
        if (await directory.FindAsync(organization, cancellationToken).ConfigureAwait(false)
                is { IsAdministrative: true }
            && await mailboxes.HeldByAsync(member, cancellationToken).ConfigureAwait(false) is Mailbox mailbox)
        {
            announced.Add(await RetiredAsync(member, mailbox, now, source, cancellationToken)
                .ConfigureAwait(false));
        }

        await audit
            .MembershipEndedAsync(organization, ended, member, acting, now, cancellationToken)
            .ConfigureAwait(false);

        foreach (DomainEvent happened in announced)
        {
            if ((await events.PublishAsync(happened, cancellationToken).ConfigureAwait(false))
                .Match(() => (Error?)null, error => error) is Error unpublished)
            {
                return Result.Failure(unpublished);
            }
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static string Key(MembershipId membership, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{Ended}:{membership.Value}@{at.UtcTicks}");

    private static string Key(IdentifierId identifier, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{identifier.Value}@{at.UtcTicks}");

    private static SendDestination Destination(HeldIdentifier identifier) =>
        identifier.Kind is IdentifierKind.Email
            ? SendDestination.Of(EmailAddress.TryParse(identifier.Canonical, out EmailAddress address)
                ? address
                : throw new InvalidOperationException("A held address is canonical already."))
            : SendDestination.Of(PhoneNumber.TryParse(identifier.Canonical, out PhoneNumber number)
                ? number
                : throw new InvalidOperationException("A held number is canonical already."));

    // REG-MAIL-003: the corporate address stops being the account's and the personal
    // email the membership kept becomes the primary in the same step; the mailbox is
    // retired, which leaves it owed disabled and ends every app password with it
    // (INT-MAIL-006a); and the set as it now stands hears of the new primary once
    // (REG-IDENT-005).
    private async ValueTask<DomainEvent> RetiredAsync(
        SubjectId member,
        Mailbox mailbox,
        DateTimeOffset now,
        string source,
        CancellationToken cancellationToken)
    {
        HeldIdentifiers held = await identifiers.HeldAsync(member, cancellationToken).ConfigureAwait(false);

        IdentifierId primary = await identifiers
            .RetireCorporateAsync(member, mailbox.Address.Value, cancellationToken)
            .ConfigureAwait(false);

        mailbox.Retire(now);

        await mailboxes.RecordAsync(mailbox, cancellationToken).ConfigureAwait(false);

        // The set as it now stands is the set as it stood less the address that left:
        // the personal email was in it already, and no backup setting changed. The
        // retired address behaves as unknown, so it is told nothing.
        _ = await TellAsync(
                [
                    .. held.NoticeSet.Where(identifier =>
                        identifier.Kind is not IdentifierKind.Email
                        || !string.Equals(identifier.Canonical, mailbox.Address.Value, StringComparison.Ordinal)),
                ],
                member,
                source,
                cancellationToken)
            .ConfigureAwait(false);

        return new IdentifierPrimaryChanged(now, Key(primary, now), primary, IdentifierKind.Email)
        {
            Subject = member,
        };
    }

    private async ValueTask<int> TellAsync(
        IReadOnlyList<HeldIdentifier> reached,
        SubjectId member,
        string source,
        CancellationToken cancellationToken)
    {
        string? settled = await identifiers.LanguageAsync(member, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(read => read, _ => (IReadOnlyList<string>)[]);

        string? language = RecipientLanguage.Of(settled, requested: null, languages);
        int told = 0;

        foreach (HeldIdentifier identifier in reached)
        {
            var request = new SendRequest(
                Destination(identifier),
                MessageKind.IdentifierSettingsChanged,
                RestrictionPurpose.Notification,
                source,
                language)
            {
                Subject = member,
                Values = Nothing,
            };

            // A security notice one destination refuses still reaches the rest: the
            // set exists so that no one channel can silence it.
            Result<SendReference> sent = await sending
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            told += sent.Match(_ => 1, _ => 0);
        }

        return told;
    }
}
