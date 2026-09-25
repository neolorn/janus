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
using Janus.Authentication.Recovery;
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
        await using ServiceProvider services = Deployed(Authorization.Deployment.Noon);

        BackgroundWorker worker = services.GetServices<IHostedService>().OfType<BackgroundWorker>().Single();

        _ = await worker.RunDueAsync(TestContext.Current.CancellationToken);

        await using NpgsqlConnection connection = await host.OpenAsync();

        IEnumerable<string> succeeded = await connection.QueryAsync<string>(
            "SELECT name FROM identity.background_jobs WHERE succeeded_at IS NOT NULL ORDER BY name COLLATE \"C\"");

        Assert.Equal(
            BackgroundJobs.All.Select(job => job.Name).Order(StringComparer.Ordinal),
            succeeded);
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
        SubjectId subject = await new Authorization.Deployment(host).AccountAsync(cancellationToken);
        byte[] holder = RandomNumberGenerator.GetBytes(32);

        await using (ServiceProvider seeding = Deployed(Authorization.Deployment.Noon))
        {
            await LapsingAsync(seeding, subject, holder, cancellationToken);
        }

        await using ServiceProvider services = Deployed(Authorization.Deployment.Noon.AddDays(40));

        _ = await services.GetServices<IHostedService>().OfType<BackgroundWorker>().Single()
            .RunDueAsync(cancellationToken);

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
    /// IDN-PRIN-003 AC4: an event every consumer has taken is a spent working artefact,
    /// so one pass of the worker, which nobody started, clears it. An event still
    /// waiting for a consumer, and one whose budget was spent, stay.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_PRIN_003_AC4_AnEventEveryConsumerTookIsClearedWithNobodyAskingAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DateTimeOffset noon = Authorization.Deployment.Noon;
        PendingEvent published = Raised(noon, publishedAt: noon, failedAt: null);
        PendingEvent waiting = Raised(noon, publishedAt: null, failedAt: null);
        PendingEvent failed = Raised(noon, publishedAt: null, failedAt: noon);

        await using (ServiceProvider seeding = Deployed(noon))
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

        await using ServiceProvider services = Deployed(noon.AddDays(1));

        _ = await services.GetServices<IHostedService>().OfType<BackgroundWorker>().Single()
            .RunDueAsync(cancellationToken);

        await using NpgsqlConnection connection = await host.OpenAsync();

        Assert.Equal(
            new[] { waiting.Id.Value, failed.Id.Value }.Order(),
            (await connection.QueryAsync<Guid>(
                "SELECT id FROM identity.events WHERE id = ANY(@ids)",
                new { ids = new[] { published.Id.Value, waiting.Id.Value, failed.Id.Value } }))
                .Order());
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
                satisfiesEveryGate: false),
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

    // A deployment over the fixture's database at one instant, with what a host declares
    // for itself: where the events go, the two transports, its sign-in screen and its
    // client.
    private ServiceProvider Deployed(DateTimeOffset now) =>
        new ServiceCollection()
            .AddSingleton<TimeProvider>(new FixedTime(now))
            .AddSingleton<IEvents>(new EventsInMemory())
            .AddSingleton<IMailTransport>(new MailTransportInMemory())
            .AddSingleton<ISmsTransport>(new SmsTransportInMemory())
            .AddSingleton(new AuthenticationAddresses(
                "https://accounts.example.test/signin",
                "https://accounts.example.test"))
            .AddSingleton(new SignOnClient("this-application"))
            .AddJanus(
                host.ConnectionString,
                new KeyEncryptionKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] }),
                new FingerprintKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] }),
                Encoding.UTF8.GetBytes("the secret this application presents"),
                Encoding.UTF8.GetBytes(host.MaintenanceConnectionString),
                HostFixture.Declaration(),
                ApplicationKind.Public)
            .BuildServiceProvider();
}
