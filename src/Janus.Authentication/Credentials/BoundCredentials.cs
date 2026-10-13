using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Passwords;
using Janus.Authentication.Recovery;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Credentials;

/// <summary>
/// What becomes of a texted second step when the number it is bound to leaves the
/// account: the credential is taken out of use at once, and a pending enrolment bound
/// to the number ends with it.
/// </summary>
/// <param name="authenticators">Where the account's credentials are held.</param>
/// <param name="pending">Where an enrolment yet to be confirmed is held.</param>
/// <param name="passwords">Where the account's password is read.</param>
/// <param name="reports">Where the reports that are running are held.</param>
/// <param name="losses">What suspends a credential for the window and tells of it.</param>
/// <param name="audit">Where what became of a credential is recorded.</param>
/// <param name="configuration">Where the window comes from.</param>
/// <remarks>
/// Implements AUTH-FACT-002, AUTH-RECOV-007, REG-IDENT-006 and REG-IDENT-007. The writer
/// that removes the number, applies a replace's swap or applies an undo that displaces
/// the number calls this inside its unit of work, having locked the identifier's row,
/// which is the lock a begin, a confirm and an ask of a code take first too; the
/// enrolment is locked next and the credentials after it, as a confirm locks them. An
/// undo restores the number and nothing here: the credential stays bound to none.
/// </remarks>
internal sealed class BoundCredentials(
    IAuthenticatorStore authenticators,
    IPendingEnrolmentStore pending,
    IPasswordStore passwords,
    ILossReportStore reports,
    LossReports losses,
    ICredentialAudit audit,
    IConfigurationStore configuration)
{
    private static readonly AuditAction Removed = AuditActions.CredentialRemoved;

    /// <summary>
    /// Takes out of use every credential bound to a number that is leaving the account,
    /// and ends a pending enrolment bound to it, inside the unit of work that takes the
    /// number away.
    /// </summary>
    /// <param name="context">Who asked for the change, or nothing where no session did.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="number">The number that is leaving, canonically.</param>
    /// <param name="now">When it leaves.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The reports of the credentials left suspended, each to be told once the unit of
    /// work has committed (<see cref="ToldAsync"/>); or the failure, on which the caller
    /// rolls back.
    /// </returns>
    /// <exception cref="ArgumentNullException">The number is absent.</exception>
    public async ValueTask<Result<IReadOnlyList<LossReport>>> ReleaseAsync(
        AccessContext? context,
        SubjectId subject,
        [NeverLogged] string number,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(number);

        Error? failure = null;

        TimeSpan window = (await configuration
                .ReadAsync(Settings.RecoveryInvalidationWindow, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IReadOnlyList<LossReport>>(failure);
        }

        // AUTH-FACT-002: the enrolment ends with the number, so its code answers
        // auth.code.expired from now, an undo notwithstanding.
        if (await pending.FindForUpdateAsync(subject, TextedEnrolments.Entry, cancellationToken).ConfigureAwait(false)
            is PendingEnrolment staged
            && string.Equals(staged.Number, number, StringComparison.Ordinal))
        {
            await pending.RemoveAsync(subject, staged.Factor, cancellationToken).ConfigureAwait(false);
        }

        List<Authenticator> enrolled =
        [
            .. await authenticators.OfForUpdateAsync(subject, cancellationToken).ConfigureAwait(false),
        ];

        bool password = await SecondStep.AvailableAsync(passwords, subject, cancellationToken)
            .ConfigureAwait(false);

        var suspended = new List<LossReport>();

        foreach (Authenticator bound in enrolled
            .Where(held => Authenticator.IsBoundToNumber(held.Factor)
                && string.Equals(held.Number, number, StringComparison.Ordinal))
            .ToList())
        {
            // AUTH-FACT-002: one a loss report or an assurance-lowering removal has
            // already suspended stays suspended and keeps its invalidation instant; it
            // is a credential whose number left the account from now.
            if (!bound.IsUsable)
            {
                bound.Unbind();

                await authenticators.RecordAsync(bound, cancellationToken).ConfigureAwait(false);

                if (bound.IsAwaitingInvalidation
                    && await reports.FindAsync(bound.Id, cancellationToken).ConfigureAwait(false) is LossReport running)
                {
                    suspended.Add(running);
                }

                continue;
            }

            // AUTH-FACT-002, AUTH-RECOV-007: an active one is removed where that leaves
            // reachable assurance unchanged.
            if (!HeldFactors.LoweredWithout(enrolled, bound, password))
            {
                _ = enrolled.Remove(bound);

                await authenticators.RemoveAsync(bound.Id, cancellationToken).ConfigureAwait(false);
                await audit.RecordedAsync(Removed, subject, bound.Id, now, cancellationToken).ConfigureAwait(false);

                continue;
            }

            // Otherwise it is suspended and invalidated after the window, as a removal
            // that would lower reachable assurance is.
            bound.Unbind();

            LossReport? opened = (await losses.OpenAsync(context, bound, window, now, cancellationToken).ConfigureAwait(false))
                .Match(report => report, error => Withheld<LossReport?>(error, ref failure));

            if (failure is not null || opened is null)
            {
                return Result.Failure<IReadOnlyList<LossReport>>(failure!);
            }

            suspended.Add(opened);
        }

        return Result.Success<IReadOnlyList<LossReport>>(suspended);
    }

    /// <summary>
    /// Tells every recorded channel of each credential the number's leaving left
    /// suspended, once the unit of work that took the number away has committed: by the
    /// notice that carries no link (AUTH-RECOV-007).
    /// </summary>
    /// <param name="suspended">The reports <see cref="ReleaseAsync"/> answered.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="now">When the number left.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing, or what kept a report from being recorded.</returns>
    /// <exception cref="ArgumentNullException">The reports are absent.</exception>
    public async ValueTask<Error?> ToldAsync(
        IReadOnlyList<LossReport> suspended,
        string? source,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(suspended);

        foreach (LossReport report in suspended)
        {
            // A report opened now counts its schedule from now; one that was running
            // keeps the schedule its suspension runs, this notice being beside it.
            DateTimeOffset at = report.ReportedAt == now ? now : report.NotifiedAt;

            if (await losses.ToldAsync(report, source, at, cancellationToken).ConfigureAwait(false) is Error untold)
            {
                return untold;
            }
        }

        return null;
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
