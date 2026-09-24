using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.BreakGlass;
using Janus.Authentication.Credentials;
using Janus.Authentication.Factors;
using Janus.Authentication.Invitations;
using Janus.Authentication.Mailboxes;
using Janus.Authentication.Maintenance;
using Janus.Authentication.Oidc;
using Janus.Authentication.Organizations;
using Janus.Authentication.Recovery;
using Janus.Authentication.Registration;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authentication.SignIn;
using Janus.Authorization.Gate;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Alerting;
using Janus.Hosting.Events;
using Janus.Hosting.Sending;
using Janus.Hosting.Sessions;
using Janus.Identity.Identifiers;
using Janus.Privacy.Erasures;
using Janus.Privacy.Outbox;
using Janus.Privacy.Requests;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace Janus.Hosting.Background;

/// <summary>
/// The scheduled work of the library, each piece as the named principal it runs as.
/// </summary>
/// <remarks>
/// Implements INF-BG-001, INF-BG-002, OPS-OBS-003 and AUTH-KEY-003. The reason each job
/// states is the item that requires it, which is what its audited actions carry.
/// </remarks>
internal static class BackgroundJobs
{
    // INT-MAIL-007: the reconciliation runs daily, which the chapter fixes and no
    // setting names. The watches over a list of dates (OPS-MAINT-001, PRIV-RIGHT-002)
    // run at the same pace, since the lead they measure is counted in days.
    private static readonly TimeSpan Daily = TimeSpan.FromDays(1);

    // INF-TLS-003: a failed renewal is seen the day it happens, and INF-HOST-001: a clock
    // drifts by seconds a day, so an hour finds either long before an outage. OPS-BOOT-001
    // AC3: a deployment without an emergency credential hears of it within the hour.
    private static readonly TimeSpan Hourly = TimeSpan.FromHours(1);

    // AUTH-OIDC-003: no refresh token outlives its session and no session outlives
    // this, so a redeemed token older than it can never be presented again and its row
    // no longer catches a second presentation.
    private static readonly TimeSpan LongestSession =
        Settings.SessionDefaultAbsolute.Ceiling ?? Settings.SessionDefaultAbsolute.Default;

    /// <summary>
    /// Every job the worker runs.
    /// </summary>
    public static IReadOnlyList<BackgroundJob> All { get; } =
    [
        BackgroundJob.Every(
            "expiry-sweep",
            "OPS-OBS-003",
            SystemOperation.ExpirySweep,
            Settings.SweepInterval,
            (services, _, cancellationToken) => SweepExpiredAsync(services, cancellationToken)),
        BackgroundJob.Every(
            "registration-sweep",
            "REG-SESS-001",
            SystemOperation.ExpirySweep,
            Settings.SweepInterval,
            async (services, context, cancellationToken) =>
            {
                _ = await services.GetRequiredService<RegistrationService>()
                    .SweepAsync(cancellationToken)
                    .ConfigureAwait(false);

                return Result.Success();
            }),
        BackgroundJob.Every(
            "invitation-sweep",
            "PRIV-RIGHT-005a",
            SystemOperation.ExpirySweep,
            Settings.SweepInterval,
            async (services, context, cancellationToken) =>
            {
                _ = await services.GetRequiredService<InvitationService>()
                    .SweepAsync(cancellationToken)
                    .ConfigureAwait(false);

                return Result.Success();
            }),
        BackgroundJob.Every(
            "account-deletion",
            "IDN-LIFE-014",
            SystemOperation.ExpirySweep,
            Settings.SweepInterval,
            async (services, context, cancellationToken) =>
            {
                _ = await services.GetRequiredService<DeletionSweep>()
                    .SweepAsync(context, cancellationToken)
                    .ConfigureAwait(false);

                return Result.Success();
            }),
        BackgroundJob.Every(
            "organization-erasure",
            "IDN-ORG-003",
            SystemOperation.ExpirySweep,
            Settings.SweepInterval,
            async (services, context, cancellationToken) => Done(
                await services.GetRequiredService<OrganizationErasureSweep>()
                    .SweepAsync(context, cancellationToken)
                    .ConfigureAwait(false))),
        BackgroundJob.Every(
            "privacy-deadlines",
            "PRIV-RIGHT-002",
            SystemOperation.ExpirySweep,
            Settings.SweepInterval,
            async (services, context, cancellationToken) =>
            {
                _ = await services.GetRequiredService<DeadlineSweep>()
                    .SweepAsync(context, cancellationToken)
                    .ConfigureAwait(false);

                return Result.Success();
            }),
        BackgroundJob.Every(
            "loss-reports",
            "AUTH-RECOV-007",
            SystemOperation.ExpirySweep,
            Settings.SweepInterval,
            async (services, context, cancellationToken) => Done(
                await services.GetRequiredService<LossReports>()
                    .AdvanceAsync(context, cancellationToken)
                    .ConfigureAwait(false))),
        BackgroundJob.Every(
            "domain-reverification",
            "REG-DOM-001",
            SystemOperation.ExpirySweep,
            Settings.SweepInterval,
            async (services, _, cancellationToken) => Done(
                await services.GetRequiredService<DomainReverification>()
                    .SweepAsync(cancellationToken)
                    .ConfigureAwait(false))),
        BackgroundJob.Every(
            "outbox",
            "IDN-LIFE-003a",
            SystemOperation.Delivery,
            Settings.OutboxPollInterval,
            async (services, context, cancellationToken) =>
            {
                _ = await services.GetRequiredService<OutboxPublisher>()
                    .PublishAsync(cancellationToken)
                    .ConfigureAwait(false);

                return Result.Success();
            }),
        BackgroundJob.Every(
            "events",
            "IDN-LIFE-003a",
            SystemOperation.Delivery,
            Settings.OutboxPollInterval,
            async (services, _, cancellationToken) => Done(
                await services.GetRequiredService<EventPublisher>()
                    .PublishAsync(cancellationToken)
                    .ConfigureAwait(false))),
        BackgroundJob.Every(
            "sends",
            "INF-BG-001",
            SystemOperation.Delivery,
            Settings.OutboxPollInterval,
            async (services, _, cancellationToken) => Done(
                await services.GetRequiredService<SendingService>()
                    .RetryAsync(cancellationToken)
                    .ConfigureAwait(false))),
        BackgroundJob.Every(
            "mailbox-provisioning",
            "INT-MAIL-006a",
            SystemOperation.Delivery,
            Settings.OutboxPollInterval,
            async (services, _, cancellationToken) => Done(
                await services.GetRequiredService<MailboxPublisher>()
                    .PublishAsync(cancellationToken)
                    .ConfigureAwait(false))),
        BackgroundJob.Every(
            "alert-dispatch",
            "OPS-ALERT-001",
            SystemOperation.Delivery,
            Settings.OutboxPollInterval,
            async (services, _, cancellationToken) => Done(
                await services.GetRequiredService<AlertDispatch>()
                    .CarryAsync(cancellationToken)
                    .ConfigureAwait(false))),
        BackgroundJob.Every(
            "mail-reconciliation",
            "INT-MAIL-007",
            SystemOperation.Reconciliation,
            Daily,
            async (services, _, cancellationToken) => Done(
                await services.GetRequiredService<MailboxReconciliation>()
                    .ReconcileAsync(cancellationToken)
                    .ConfigureAwait(false))),
        BackgroundJob.Every(
            "location-database",
            "INT-GEN-006",
            SystemOperation.Monitoring,
            Settings.LocationDatabaseRefresh,
            async (services, _, cancellationToken) => await services.GetRequiredService<LocationDatabase>()
                .RefreshAsync(cancellationToken)
                .ConfigureAwait(false)),
        BackgroundJob.Every(
            "read-volume-baseline",
            "OPS-ALERT-005",
            SystemOperation.Monitoring,
            Daily,
            async (services, _, cancellationToken) => Done(
                await services.GetRequiredService<ReadVolume>()
                    .RebaselineAsync(cancellationToken)
                    .ConfigureAwait(false))),
        BackgroundJob.Every(
            "holiday-list",
            "PRIV-RIGHT-002",
            SystemOperation.Monitoring,
            Daily,
            async (services, _, cancellationToken) => Done(
                await services.GetRequiredService<HolidayListWatch>()
                    .WatchAsync(cancellationToken)
                    .ConfigureAwait(false))),
        BackgroundJob.Every(
            "emergency-credential",
            "OPS-BOOT-001",
            SystemOperation.Monitoring,
            Hourly,
            async (services, _, cancellationToken) => await services.GetRequiredService<EmergencyCredentialWatch>()
                .WatchAsync(cancellationToken)
                .ConfigureAwait(false)),
        BackgroundJob.Every(
            "clock-drift",
            "INF-HOST-001",
            SystemOperation.Monitoring,
            Hourly,
            async (services, _, cancellationToken) => await services.GetRequiredService<ClockDriftWatch>()
                .WatchAsync(cancellationToken)
                .ConfigureAwait(false)),
        BackgroundJob.Every(
            "certificate-renewal",
            "INF-TLS-003",
            SystemOperation.Monitoring,
            Hourly,
            async (services, _, cancellationToken) => await services.GetRequiredService<CertificateRenewalWatch>()
                .WatchAsync(cancellationToken)
                .ConfigureAwait(false)),
        BackgroundJob.Every(
            "licence-expiry",
            "OPS-MAINT-001",
            SystemOperation.Monitoring,
            Daily,
            async (services, _, cancellationToken) => Done(
                await services.GetRequiredService<LicenceExpiry>()
                    .WarnAsync(cancellationToken)
                    .ConfigureAwait(false))),
        BackgroundJob.Every(
            "sms-balance",
            "INT-SMS-004",
            SystemOperation.Monitoring,
            Settings.AbuseSmsPollInterval,
            (services, _, cancellationToken) => PollBalanceAsync(services, cancellationToken)),
    ];

    // AUTH-KEY-003, OPS-OBS-003: what has expired goes, each kind in a statement of its
    // own, so one pass leaves nothing half removed.
    private static async ValueTask<Result> SweepExpiredAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = services.GetRequiredService<TimeProvider>().GetUtcNow();

        _ = await services.GetRequiredService<ISessionStore>()
            .SweepAsync(now, cancellationToken).ConfigureAwait(false);
        _ = await services.GetRequiredService<IPreAuthenticationStore>()
            .SweepAsync(now, cancellationToken).ConfigureAwait(false);
        _ = await services.GetRequiredService<IChallengeStore>()
            .SweepAsync(now, cancellationToken).ConfigureAwait(false);
        _ = await services.GetRequiredService<IPendingSignInStore>()
            .SweepAsync(now, cancellationToken).ConfigureAwait(false);
        _ = await services.GetRequiredService<IVerificationCodeStore>()
            .SweepAsync(now, cancellationToken).ConfigureAwait(false);
        _ = await services.GetRequiredService<IKeyCeremonyStore>()
            .SweepAsync(now, cancellationToken).ConfigureAwait(false);
        _ = await services.GetRequiredService<IRecoveryLinkStore>()
            .SweepAsync(now, cancellationToken).ConfigureAwait(false);
        _ = await services.GetRequiredService<IIdentifierStore>()
            .SweepRemovalsAsync(now, cancellationToken).ConfigureAwait(false);
        _ = await services.GetRequiredService<BreakGlassService>()
            .SweepAsync(cancellationToken).ConfigureAwait(false);
        _ = await services.GetRequiredService<SigningKeys>()
            .SweepAsync(cancellationToken).ConfigureAwait(false);
        _ = await services.GetRequiredService<IOpenIddictTokenManager>()
            .PruneAsync(now - LongestSession, cancellationToken).ConfigureAwait(false);
        _ = await services.GetRequiredService<IOpenIddictAuthorizationManager>()
            .PruneAsync(now - LongestSession, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    // LIB-HOST-001: the transport is the deployment's, and one that registered none
    // has no balance to read.
    private static async ValueTask<Result> PollBalanceAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (services.GetService<ISmsTransport>() is null)
        {
            return Result.Success();
        }

        return Done(await services.GetRequiredService<SmsBalance>()
            .PollAsync(cancellationToken)
            .ConfigureAwait(false));
    }

    private static Result Done<TValue>(Result<TValue> outcome) =>
        outcome.Match(_ => Result.Success(), Result.Failure);
}
