using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication;
using Janus.Authentication.Events;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Recovery;
using Janus.Authentication.Registration;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Hosting.Background;
using Janus.Hosting.Bff;
using Janus.Hosting.Tests.Authorization;
using Janus.Identity.Identifiers;
using Janus.Privacy.SubjectKeys;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Background;

/// <summary>
/// The library's scheduled work as a deployment registers it through its one entry
/// point, run over the database (INF-BG-001, OPS-OBS-003).
/// </summary>
[Trait("kind", "integration")]
public sealed class BackgroundJobsTests(HostFixture host) : IClassFixture<HostFixture>
{
    /// <summary>
    /// INF-BG-001 AC1, OPS-OBS-003 AC1: the worker the entry point registers runs every
    /// job with nobody asking, and each one's success is on record, so no cleanup is
    /// left to a person.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_BG_001_AC1_EveryJobRunsWithoutAPersonAsync()
    {
        await ForgetEarlierRunsAsync();

        await using ServiceProvider services = Deployed(host, Authorization.Deployment.Noon);

        BackgroundWorker worker = services.GetServices<IHostedService>().OfType<BackgroundWorker>().Single();

        _ = await worker.RunDueAsync(TestContext.Current.CancellationToken);

        await worker.SettledAsync(TestContext.Current.CancellationToken);

        await using NpgsqlConnection connection = await host.OpenAsync();

        IEnumerable<string> succeeded = await connection.QueryAsync<string>(
            "SELECT name FROM identity.background_jobs WHERE succeeded_at IS NOT NULL ORDER BY name COLLATE \"C\"");

        Assert.Equal(
            BackgroundJobs.All.Select(job => job.Name).Order(StringComparer.Ordinal),
            succeeded);
    }

    /// <summary>
    /// AUTH-ABUSE-008 AC6, LIB-HOST-001 AC7: in a deployment that declared no range
    /// source and counts <c>datacenterRange</c>, a pass of the worker leaves
    /// <c>degradation</c> on record under <c>botdefence.ranges.absent</c>, raised by the
    /// run of <c>datacenter-ranges</c> itself, though no registration arrived.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_008_AC6_ARunOfTheRangesJobRaisesTheAbsenceThoughNoRegistrationArrivesAsync()
    {
        await ForgetEarlierRunsAsync();

        // The pass carries what it raises where alert-dispatch takes its turn after the
        // ranges job's, and a pass of another test, at a later instant of its own, would
        // fold it there.
        await using (NpgsqlConnection earlier = await host.OpenAsync())
        {
            await earlier.ExecuteAsync("DELETE FROM identity.alerts;");
        }

        DateTimeOffset at = Authorization.Deployment.Noon.AddDays(3);

        await using ServiceProvider services = Deployed(host, at);

        BackgroundWorker worker = services.GetServices<IHostedService>().OfType<BackgroundWorker>().Single();

        _ = await worker.RunDueAsync(TestContext.Current.CancellationToken);

        await worker.SettledAsync(TestContext.Current.CancellationToken);

        await using NpgsqlConnection connection = await host.OpenAsync();

        Assert.Equal(
            1,
            await connection.ExecuteScalarAsync<int>(
                """
                SELECT
                    (SELECT count(*) FROM identity.raised_alerts
                     WHERE condition = 'degradation' AND scope = 'botdefence.ranges.absent' AND raised_at = @at)::int
                    + (SELECT count(*) FROM identity.alerts
                       WHERE key = 'degradation:botdefence.ranges.absent' AND at = @at)::int
                """,
                new { at }));
    }

    /// <summary>
    /// OPS-OBS-003 AC1: a session past its lifetime, a one-time code and a recovery token
    /// past their expiry, and a given-up identifier past its undo window are gone after
    /// one pass of the worker, which nobody started.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_OBS_003_AC1_WhatHasLapsedIsClearedWithNobodyAskingAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await ForgetEarlierRunsAsync();

        SubjectId subject = await new Authorization.Deployment(host).AccountAsync(cancellationToken);
        byte[] holder = RandomNumberGenerator.GetBytes(32);

        await using (ServiceProvider seeding = Deployed(host, Authorization.Deployment.Noon))
        {
            await LapsingAsync(seeding, subject, holder, cancellationToken);
        }

        await using ServiceProvider services = Deployed(host, Authorization.Deployment.Noon.AddDays(40));

        BackgroundWorker worker = services.GetServices<IHostedService>().OfType<BackgroundWorker>().Single();

        _ = await worker.RunDueAsync(cancellationToken);
        await worker.SettledAsync(TestContext.Current.CancellationToken);

        await using NpgsqlConnection connection = await host.OpenAsync();

        Assert.Equal(
            (0, 0, 0, 0),
            (await connection.ExecuteScalarAsync<int>(
                    "SELECT count(*) FROM identity.sessions WHERE subject = @subject",
                    new { subject = subject.Value }),
                await connection.ExecuteScalarAsync<int>(
                    "SELECT count(*) FROM identity.verification_codes WHERE holder = @holder",
                    new { holder }),
                await connection.ExecuteScalarAsync<int>(
                    "SELECT count(*) FROM identity.recovery_links WHERE subject = @subject",
                    new { subject = subject.Value }),
                await connection.ExecuteScalarAsync<int>(
                    "SELECT count(*) FROM identity.identifier_removals WHERE subject = @subject",
                    new { subject = subject.Value })));
    }

    /// <summary>
    /// OPS-OBS-003 AC1, REG-IDENT-004 AC4 (D-166, 306): an identifier's add whose code is
    /// past its lifetime is gone after one pass of the worker, which nobody started, and
    /// one whose code still stands is kept.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_OBS_003_AC1_AnAddPastItsCodesLifetimeIsClearedWithNobodyAskingAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await ForgetEarlierRunsAsync();

        DateTimeOffset noon = Authorization.Deployment.Noon;
        SubjectId subject = await new Authorization.Deployment(host).AccountAsync(cancellationToken);
        var lapsed = IdentifierId.New(TimeProvider.System);
        var standing = IdentifierId.New(TimeProvider.System);

        await using (ServiceProvider seeding = Deployed(host, noon))
        {
            await using AsyncServiceScope scope = seeding.CreateAsyncScope();
            IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            IPendingVerificationStore pending = scope.ServiceProvider.GetRequiredService<IPendingVerificationStore>();
            IVerificationCodeStore codes = scope.ServiceProvider.GetRequiredService<IVerificationCodeStore>();

            await work.BeginAsync(cancellationToken);
            await scope.ServiceProvider.GetRequiredService<ISubjectKeyStore>().CreateAsync(subject, cancellationToken);

            foreach ((IdentifierId staged, TimeSpan lifetime) in new[]
            {
                (lapsed, TimeSpan.FromMinutes(10)),
                (standing, TimeSpan.FromDays(2)),
            })
            {
                string value = staged.Value.ToString("N") + "@example.test";

                await pending.AddAsync(
                    PendingVerification.ToAdd(
                        subject,
                        browser: null,
                        StagedIdentity.Of(staged, IdentifierKind.Email, value, value),
                        noon),
                    cancellationToken);
                await codes.AddAsync(
                    VerificationCode.Issue(PendingVerification.CodeHolder(staged), "123456", noon, lifetime),
                    cancellationToken);
            }

            await work.CommitAsync(cancellationToken);
        }

        await using ServiceProvider services = Deployed(host, noon.AddDays(1));

        BackgroundWorker worker = services.GetServices<IHostedService>().OfType<BackgroundWorker>().Single();

        _ = await worker.RunDueAsync(cancellationToken);
        await worker.SettledAsync(TestContext.Current.CancellationToken);

        await using NpgsqlConnection connection = await host.OpenAsync();

        Assert.Equal(
            [standing.Value],
            await connection.QueryAsync<Guid>(
                "SELECT identifier_id FROM identity.identifier_verifications WHERE identifier_id = ANY(@ids)",
                new { ids = new[] { lapsed.Value, standing.Value } }));
    }

    /// <summary>
    /// IDN-PRIN-003 AC4: an event every consumer has taken is a spent working artefact,
    /// so one pass of the worker, which nobody started, clears it. An event still
    /// waiting for a consumer, and one whose budget was spent, stay.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_PRIN_003_AC4_AnEventEveryConsumerTookIsClearedWithNobodyAskingAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await ForgetEarlierRunsAsync();

        DateTimeOffset noon = Authorization.Deployment.Noon;
        PendingEvent published = Raised(noon, publishedAt: noon, failedAt: null);
        PendingEvent waiting = Raised(noon, publishedAt: null, failedAt: null);
        PendingEvent failed = Raised(noon, publishedAt: null, failedAt: noon);

        await using (ServiceProvider seeding = Deployed(host, noon))
        {
            await using AsyncServiceScope scope = seeding.CreateAsyncScope();
            IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            IPendingEvents events = scope.ServiceProvider.GetRequiredService<IPendingEvents>();

            await work.BeginAsync(cancellationToken);

            foreach (PendingEvent pending in new[] { published, waiting, failed })
            {
                await events.AddAsync(pending, cancellationToken);
            }

            await work.CommitAsync(cancellationToken);
        }

        await using ServiceProvider services = Deployed(host, noon.AddDays(1));

        BackgroundWorker worker = services.GetServices<IHostedService>().OfType<BackgroundWorker>().Single();

        _ = await worker.RunDueAsync(cancellationToken);
        await worker.SettledAsync(TestContext.Current.CancellationToken);

        await using NpgsqlConnection connection = await host.OpenAsync();

        Assert.Equal(
            new[] { waiting.Id.Value, failed.Id.Value }.Order(),
            (await connection.QueryAsync<Guid>(
                "SELECT id FROM identity.events WHERE id = ANY(@ids)",
                new { ids = new[] { published.Id.Value, waiting.Id.Value, failed.Id.Value } }))
                .Order());
    }

    /// <summary>
    /// IDN-PRIN-003 AC4 and OPS-ALERT-006: an export admitted more than an hour ago is
    /// counted by no limit again, so one pass of the worker, which nobody started, clears
    /// it; one inside the hour stays.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_PRIN_003_AC4_AnExportPastItsHourIsClearedWithNobodyAskingAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await ForgetEarlierRunsAsync();

        DateTimeOffset noon = Authorization.Deployment.Noon;
        var spent = Guid.CreateVersion7();
        var counted = Guid.CreateVersion7();

        await using (NpgsqlConnection seeding = await host.OpenAsync())
        {
            _ = await seeding.ExecuteAsync(
                """
                INSERT INTO identity.bulk_exports (id, actor, principal, admitted_at)
                VALUES (@spent, NULL, 'an-exporting-principal', @past),
                       (@counted, NULL, 'an-exporting-principal', @recent);
                """,
                new
                {
                    spent,
                    counted,
                    past = (noon - TimeSpan.FromMinutes(61)).UtcDateTime,
                    recent = (noon - TimeSpan.FromMinutes(59)).UtcDateTime,
                });
        }

        await using ServiceProvider services = Deployed(host, noon);

        BackgroundWorker worker = services.GetServices<IHostedService>().OfType<BackgroundWorker>().Single();

        _ = await worker.RunDueAsync(cancellationToken);
        await worker.SettledAsync(TestContext.Current.CancellationToken);

        await using NpgsqlConnection connection = await host.OpenAsync();

        Assert.Equal(
            [counted],
            await connection.QueryAsync<Guid>(
                "SELECT id FROM identity.bulk_exports WHERE id = ANY(@ids)",
                new { ids = new[] { spent, counted } }));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9, INF-BG-001: the worker carries a due outbox row under a claim
    /// and writes its outcome under it, which releases the claim; a row another pass
    /// holds is left as it stands.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_TheWorkerCarriesAnOutboxRowUnderItsClaimAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await ForgetEarlierRunsAsync();

        DateTimeOffset noon = Authorization.Deployment.Noon;
        SubjectId subject = await new Authorization.Deployment(host).AccountAsync(cancellationToken);
        var due = Guid.CreateVersion7(noon);
        var held = Guid.CreateVersion7(noon.AddSeconds(1));

        await using (NpgsqlConnection seeding = await host.OpenAsync())
        {
            await seeding.ExecuteAsync(
                """
                INSERT INTO identity.outbox
                    (id, subject, kind, raised_at, restricted, reason, status, attempts, next_attempt_at, claimed_until)
                VALUES (@due, @subject, 'restriction-changed', @noon, true, 'erasure-request', 'awaiting-subscribers', 0, @noon, NULL),
                       (@held, @subject, 'restriction-changed', @noon, true, 'erasure-request', 'awaiting-subscribers', 0, @noon, @later);
                """,
                new
                {
                    due,
                    held,
                    subject = subject.Value,
                    noon = noon.UtcDateTime,
                    later = noon.AddMinutes(1).UtcDateTime,
                });
        }

        await using ServiceProvider services = Deployed(host, noon);

        BackgroundWorker worker = services.GetServices<IHostedService>().OfType<BackgroundWorker>().Single();

        _ = await worker.RunDueAsync(cancellationToken);
        await worker.SettledAsync(TestContext.Current.CancellationToken);

        await using NpgsqlConnection connection = await host.OpenAsync();

        Assert.Equal(
            [("complete", 1, false), ("awaiting-subscribers", 0, true)],
            (await connection.QueryAsync<(string Status, int Attempts, bool Claimed)>(
                """
                SELECT status, attempts, claimed_until IS NOT NULL
                  FROM identity.outbox
                 WHERE id = ANY(@ids)
                 ORDER BY id
                """,
                new { ids = new[] { due, held } })).ToList());
    }

    /// <summary>
    /// IDN-PRIN-001 AC3 (D-166, 304): every job hands the context it is run as to the
    /// method its work runs, and that method refuses a principal whose operation is not
    /// the job's before it reads or changes anything.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_PRIN_001_AC3_EveryJobRefusesAPrincipalOfAnotherOperationAsync()
    {
        await using ServiceProvider services = Deployed(host, Authorization.Deployment.Noon);

        var other = AccessContext.Of(
            SystemPrincipal.ForDeployment("bootstrap", "OPS-BOOT-001", SystemOperation.Bootstrap));
        var refused = new List<string>();

        foreach (BackgroundJob job in BackgroundJobs.All)
        {
            await using AsyncServiceScope scope = services.CreateAsyncScope();

            try
            {
                _ = await job.RunAsync(scope.ServiceProvider, other, TestContext.Current.CancellationToken);
            }
            catch (ArgumentException refusal) when (refusal.ParamName == "context")
            {
                refused.Add(job.Name);
            }
        }

        Assert.Equal(BackgroundJobs.All.Select(job => job.Name), refused);
    }

    /// <summary>
    /// INT-SMS-004: no poll succeeds without a balance read, so in a deployment that
    /// registered no SMS transport the balance poll fails, naming the transport, and no
    /// success is recorded for it; its lapse then raises <c>background-job-failed</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_SMS_004_TheBalancePollFailsWithoutATransportAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await ForgetEarlierRunsAsync();

        BackgroundJob poll = BackgroundJobs.All.Single(job => job.Name == "sms-balance");

        await using ServiceProvider services = Deployed(host, Authorization.Deployment.Noon, sms: false);
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        Error refusal = (await poll.RunAsync(scope.ServiceProvider, AccessContext.Of(poll.Principal), cancellationToken)).Match(
            () => throw new Xunit.Sdk.XunitException("The poll succeeded."),
            error => error);

        BackgroundWorker worker = services.GetServices<IHostedService>().OfType<BackgroundWorker>().Single();

        _ = await worker.RunDueAsync(cancellationToken);
        await worker.SettledAsync(TestContext.Current.CancellationToken);

        await using NpgsqlConnection connection = await host.OpenAsync();

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refusal.Code);
        Assert.Equal("smsTransport", refusal.Details["key"].GetString());
        Assert.Null(await connection.ExecuteScalarAsync<DateTimeOffset?>(
            "SELECT succeeded_at FROM identity.background_jobs WHERE name = 'sms-balance'"));
    }

    // Each case owns its job state: the runs another case recorded, at its own clock, are
    // removed first, so every job is due at this case's clock and no case reads another's
    // run, whatever order the class runs in.
    private async Task ForgetEarlierRunsAsync()
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        await connection.ExecuteAsync("DELETE FROM identity.background_jobs;");
    }

    // An event as a pass left it, its next pass a year away so no pass in the test
    // offers it again.
    private static PendingEvent Raised(DateTimeOffset at, DateTimeOffset? publishedAt, DateTimeOffset? failedAt) =>
        PendingEvent.Existing(
            PendingEventId.Of(at),
            new AccountSuspended(at, Guid.NewGuid().ToString(), SuspensionOrigin.Administrator),
            attempts: 1,
            at.AddYears(1),
            new HashSet<string>(StringComparer.Ordinal),
            publishedAt,
            failedAt);

    // One of each thing the sweep clears, written through the deployment's own stores at
    // noon, each lapsing within a few days: the session under its person's key, which an
    // account written directly does not yet have, and the address given up beside the
    // primary, which is never given up.
    private static async Task LapsingAsync(
        ServiceProvider seeding,
        SubjectId subject,
        byte[] holder,
        CancellationToken cancellationToken)
    {
        DateTimeOffset noon = Authorization.Deployment.Noon;

        await using AsyncServiceScope scope = seeding.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;
        IUnitOfWork work = services.GetRequiredService<IUnitOfWork>();
        IIdentifierStore identifiers = services.GetRequiredService<IIdentifierStore>();
        Identifier kept = Email(subject, "kept@example.test");
        Identifier given = Email(subject, "given@example.test");

        await work.BeginAsync(cancellationToken);
        await services.GetRequiredService<ISubjectKeyStore>().CreateAsync(subject, cancellationToken);
        await services.GetRequiredService<ISessionStore>().AddAsync(
            Session.Begin(
                SessionId.New(TimeProvider.System),
                subject,
                new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
                new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
                noon,
                TimeSpan.FromHours(1),
                TimeSpan.FromDays(1),
                breakGlassReason: null),
            RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(32),
            cancellationToken);
        await services.GetRequiredService<IVerificationCodeStore>().AddAsync(
            VerificationCode.Issue(holder, "123456", noon, TimeSpan.FromMinutes(10)),
            cancellationToken);
        await services.GetRequiredService<IRecoveryLinkStore>().ReplaceAsync(
            RecoveryLink.Issue(
                OpaqueToken.Of("a-token-nobody-used"),
                subject,
                RecoveryPurpose.SelfService,
                noon,
                TimeSpan.FromHours(1)),
            cancellationToken);

        IdentifierSet set = await identifiers.FindBySubjectAsync(subject, cancellationToken);

        set.Add(kept, maximum: 5);
        set.Add(given, maximum: 5);

        await identifiers.RecordAsync(set, cancellationToken);
        await work.CommitAsync(cancellationToken);

        await work.BeginAsync(cancellationToken);

        set = await identifiers.FindBySubjectAsync(subject, cancellationToken);
        set.Verify(kept.Id, noon);
        set.Verify(given.Id, noon);

        await identifiers.RecordRemovalAsync(
            IdentifierRemoval.Of(set.Remove(given.Id), noon, noon.AddHours(72), [7, 3, 9]),
            cancellationToken);
        await identifiers.RecordAsync(set, cancellationToken);
        await work.CommitAsync(cancellationToken);
    }

    private static Identifier Email(SubjectId subject, string entered)
    {
        Assert.True(EmailAddress.TryParse(entered, out EmailAddress address));

        return Identifier.Email(IdentifierId.New(TimeProvider.System), subject, address, entered, Authorization.Deployment.Noon);
    }

    /// <summary>
    /// A deployment over the fixture's database at one instant, with what a host
    /// declares for itself: where the events go, the two transports (the text one only
    /// where the case keeps it), its sign-in screen and its client.
    /// </summary>
    /// <param name="host">The database.</param>
    /// <param name="now">The instant.</param>
    /// <param name="sms">Whether the text transport is registered.</param>
    /// <returns>The deployment, its key ring filled.</returns>
    internal static ServiceProvider Deployed(HostFixture host, DateTimeOffset now, bool sms = true)
    {
        var services = new ServiceCollection();

        if (sms)
        {
            services.AddSingleton<ISmsTransport>(new SmsTransportInMemory());
        }

        ServiceProvider deployed = services
            .AddSingleton<TimeProvider>(new FixedTime(now))
            .AddSingleton<IMailTransport>(new MailTransportInMemory())
            .AddSingleton(new AuthenticationAddresses(
                "https://accounts.example.test/signin",
                "https://accounts.example.test"))
            .AddSingleton(Landing.Origins)
            .AddSingleton(new SignOnClient("this-application"))
            .AddSingleton<ISecretSource>(HostFixture.Secrets(host.MaintenanceConnectionString))
            .AddJanus(host.ConnectionString, HostFixture.Declaration(), ApplicationKind.Public)
            .BuildServiceProvider();

        // CONV-DESIGN-007: the key ring is filled and the mail server in use chosen
        // before any job asks either, which is what its hosted service does in a
        // deployment that a web server starts.
        KeyRingService ring = deployed.GetServices<IHostedService>().OfType<KeyRingService>().Single();
        ring.StartingAsync(CancellationToken.None).GetAwaiter().GetResult();
        ring.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

        return deployed;
    }
}
