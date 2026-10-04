using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Events;
using Janus.Core;
using Janus.Storage.Authentication.Events;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What waits between the transaction that made an event true and the consumers the
/// host registered for it (LIB-API-001, CONV-DESIGN-002).
/// </summary>
/// <remarks>
/// One database serves the class and a pass reads every waiting row, so each test
/// begins by emptying the table.
/// </remarks>
[Trait("kind", "integration")]
public sealed class PendingEventsTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private static readonly SubjectId Subject = new(Guid.Parse("0b6f2a53-6d4e-4a8c-9d0e-2f1a7c3b5e91"));
    private static readonly SubjectId Actor = new(Guid.Parse("5c1e9f47-2a3b-4d6e-8f0a-1b2c3d4e5f60"));

    /// <summary>
    /// LIB-API-001: every event the library emits is written in its transaction and read
    /// back as it was raised, the identities it names included, so a retry days later
    /// offers what the transaction raised. An event the library adds without teaching
    /// the store its name fails here.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_API_001_EveryEmittedEventReadsBackAsItWasRaisedAsync()
    {
        await EmptiedAsync();

        List<DomainEvent> emitted = Emitted();

        Assert.Equal(
            typeof(DomainEvent).Assembly.GetExportedTypes()
                .Where(type => type.IsSubclassOf(typeof(DomainEvent)) && !type.IsAbstract)
                .Select(type => type.Name)
                .Order(StringComparer.Ordinal),
            emitted.Select(raised => raised.GetType().Name).Order(StringComparer.Ordinal));

        await using (StoreContext writing = database.Context())
        {
            var events = new PendingEvents(writing);

            foreach (DomainEvent raised in emitted)
            {
                await events.AddAsync(PendingEvent.Of(raised), TestContext.Current.CancellationToken);
            }

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        IReadOnlyList<PendingEvent> due = await DueAsync(reading, Noon, emitted.Count);

        Assert.Equal(emitted.Count, due.Count);

        foreach (DomainEvent raised in emitted)
        {
            DomainEvent held = Assert.Single(due, pending => pending.Raised.IdempotencyKey == raised.IdempotencyKey).Raised;

            if (raised is AlertRaised alert)
            {
                AlertRaised heldAlert = Assert.IsType<AlertRaised>(held);

                Assert.Equal(alert with { Details = heldAlert.Details }, heldAlert);
                Assert.Equal("sms", heldAlert.Details["gateway"].GetString());
            }
            else
            {
                Assert.Equal(raised, held);
            }
        }
    }

    /// <summary>
    /// CONV-DESIGN-002, LIB-API-001: an event published inside a transaction that rolls
    /// back leaves no row, so no consumer hears of a change that did not happen; one
    /// whose transaction commits waits for the publisher.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_002_AnEventWaitsOnlyOnceItsTransactionCommitsAsync()
    {
        await EmptiedAsync();

        await using (StoreContext abandoning = database.Context())
        {
            await using var work = new UnitOfWork(abandoning);

            await work.BeginAsync(TestContext.Current.CancellationToken);

            Assert.True((await new EventOutbox(new PendingEvents(abandoning), work)
                .PublishAsync(new AccountReactivated(Noon, "abandoned"), TestContext.Current.CancellationToken))
                .Match(() => true, _ => false));
        }

        await using (StoreContext committing = database.Context())
        {
            await using var work = new UnitOfWork(committing);

            await work.BeginAsync(TestContext.Current.CancellationToken);

            Assert.True((await new EventOutbox(new PendingEvents(committing), work)
                .PublishAsync(new AccountReactivated(Noon, "committed"), TestContext.Current.CancellationToken))
                .Match(() => true, _ => false));

            await work.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Equal(
            "committed",
            Assert.Single(await DueAsync(reading, Noon, 10)).Raised.IdempotencyKey);
    }

    /// <summary>
    /// CONV-DESIGN-002, IDN-LIFE-003a: what a pass made of an event is kept, the
    /// consumers that took it among it; a marked or failed event is not read again,
    /// and one whose next attempt lies ahead waits for it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003a_AMarkedOrFailedEventIsNotReadAgainAsync()
    {
        await EmptiedAsync();

        var marked = PendingEvent.Of(new AccountReactivated(Noon, "marked"));
        var failed = PendingEvent.Of(new AccountReactivated(Noon, "failed"));
        var waiting = PendingEvent.Of(new AccountReactivated(Noon, "waiting"));

        await using (StoreContext writing = database.Context())
        {
            var events = new PendingEvents(writing);

            await events.AddAsync(marked, TestContext.Current.CancellationToken);
            await events.AddAsync(failed, TestContext.Current.CancellationToken);
            await events.AddAsync(waiting, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        marked.Take("Host.Welcome");
        marked.Published(Noon);
        Assert.True(failed.Refused(Noon, TimeSpan.FromSeconds(30), 2.0m, maximum: 1, jitter: 1));
        waiting.Take("Host.Welcome");
        Assert.False(waiting.Refused(Noon, TimeSpan.FromSeconds(30), 2.0m, maximum: 3, jitter: 1));

        await using (StoreContext recording = database.Context())
        {
            var events = new PendingEvents(recording);

            foreach (PendingEvent pending in new[] { marked, failed, waiting })
            {
                EventClaim claim = await events.ClaimAsync(pending.Id, Noon, Timeout, TestContext.Current.CancellationToken)
                    ?? throw new Xunit.Sdk.XunitException("The event was not claimed.");

                Assert.True(await events.RecordAsync(pending, claim, TestContext.Current.CancellationToken));
            }
        }

        await using StoreContext reading = database.Context();

        Assert.Empty(await DueAsync(reading, Noon.AddSeconds(29), 10));

        PendingEvent due = Assert.Single(await DueAsync(reading, Noon.AddSeconds(30), 10));

        Assert.Equal(waiting.Id, due.Id);
        Assert.Equal(1, due.Attempts);
        Assert.Equal(["Host.Welcome"], due.Taken);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9, INF-BG-001 AC4: an event row is claimed whole by one
    /// conditional update, so of several passes that reach it at once one takes it; while
    /// the claim stands no pass reads the row as due and none claims it; a renewal moves
    /// the claim's end and is the pass's own alone; and once the claim has timed out the
    /// next pass takes the row over, after which the first pass renews, takes and records
    /// nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_AnEventIsClaimedByOnePassAndWrittenOnlyUnderItsClaimAsync()
    {
        await EmptiedAsync();

        var raised = PendingEvent.Of(new AccountReactivated(Noon, "claimed"));

        await AddedAsync(raised);

        EventClaim?[] claims = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ClaimedAsync(raised.Id, Noon)));

        EventClaim claim = Assert.Single(claims, one => one is not null)!.Value;

        Assert.Equal(Noon + Timeout, claim.Until);
        Assert.Null(await ClaimedAsync(raised.Id, Noon + Timeout - TimeSpan.FromSeconds(1)));

        await using (StoreContext reading = database.Context())
        {
            Assert.Empty(await DueAsync(reading, Noon + Timeout - TimeSpan.FromSeconds(1), 10));
        }

        EventClaim renewed;

        await using (StoreContext renewing = database.Context())
        {
            renewed = await new PendingEvents(renewing)
                .RenewAsync(claim, Noon.AddMinutes(1), Timeout, TestContext.Current.CancellationToken)
                ?? throw new Xunit.Sdk.XunitException("The claim was not renewed.");

            Assert.Equal(Noon.AddMinutes(1) + Timeout, renewed.Until);
            Assert.Null(await new PendingEvents(renewing)
                .RenewAsync(claim, Noon.AddMinutes(1), Timeout, TestContext.Current.CancellationToken));
        }

        Assert.Null(await ClaimedAsync(raised.Id, Noon + Timeout));

        raised.Take("Host.Welcome");

        await using (StoreContext taking = database.Context())
        {
            Assert.False(await new PendingEvents(taking).TakeAsync(raised, claim, TestContext.Current.CancellationToken));
            Assert.True(await new PendingEvents(taking).TakeAsync(raised, renewed, TestContext.Current.CancellationToken));
        }

        EventClaim taken = await ClaimedAsync(raised.Id, renewed.Until)
            ?? throw new Xunit.Sdk.XunitException("The timed-out claim was not taken over.");

        raised.Take("Host.Ledger");
        raised.Published(renewed.Until);

        await using (StoreContext recording = database.Context())
        {
            var events = new PendingEvents(recording);

            Assert.Null(await events.RenewAsync(renewed, renewed.Until, Timeout, TestContext.Current.CancellationToken));
            Assert.False(await events.TakeAsync(raised, renewed, TestContext.Current.CancellationToken));
            Assert.False(await events.RecordAsync(raised, renewed, TestContext.Current.CancellationToken));

            PendingEvent held = await events.FindAsync(raised.Id, TestContext.Current.CancellationToken)
                ?? throw new Xunit.Sdk.XunitException("The event was not written.");

            Assert.Equal(["Host.Welcome"], held.Taken);
            Assert.Null(held.PublishedAt);
            Assert.True(await events.RecordAsync(raised, taken, TestContext.Current.CancellationToken));
            Assert.False(await events.RecordAsync(raised, taken, TestContext.Current.CancellationToken));
        }

        await using StoreContext after = database.Context();

        PendingEvent marked = await new PendingEvents(after).FindAsync(raised.Id, TestContext.Current.CancellationToken)
            ?? throw new Xunit.Sdk.XunitException("The event was not written.");

        Assert.Equal(renewed.Until, marked.PublishedAt);
        Assert.Equal(["Host.Ledger", "Host.Welcome"], marked.Taken.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9: a claim succeeds only where the row's next attempt is due, so
    /// an event a pass released and rescheduled is not claimed before that instant, and a
    /// marked or failed event is never claimed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_AnEventReleasedAndRescheduledIsNotClaimedBeforeItIsDueAsync()
    {
        await EmptiedAsync();

        var rescheduled = PendingEvent.Of(new AccountReactivated(Noon, "rescheduled"));
        var failed = PendingEvent.Of(new AccountReactivated(Noon, "spent"));

        await AddedAsync(rescheduled);
        await AddedAsync(failed);

        Assert.False(rescheduled.Refused(Noon, TimeSpan.FromMinutes(10), 2.0m, maximum: 3, jitter: 1));
        Assert.True(failed.Refused(Noon, TimeSpan.FromMinutes(10), 2.0m, maximum: 1, jitter: 1));

        await using (StoreContext recording = database.Context())
        {
            var events = new PendingEvents(recording);

            foreach (PendingEvent pending in new[] { rescheduled, failed })
            {
                EventClaim claim = await events.ClaimAsync(pending.Id, Noon, Timeout, TestContext.Current.CancellationToken)
                    ?? throw new Xunit.Sdk.XunitException("The event was not claimed.");

                Assert.True(await events.RecordAsync(pending, claim, TestContext.Current.CancellationToken));
            }
        }

        Assert.Null(await ClaimedAsync(rescheduled.Id, Noon.AddMinutes(10) - TimeSpan.FromSeconds(1)));
        Assert.Null(await ClaimedAsync(failed.Id, Noon.AddDays(1)));
        Assert.NotNull(await ClaimedAsync(rescheduled.Id, Noon.AddMinutes(10)));
    }

    // One of each event the library emits, each carrying a value in every field.
    private static List<DomainEvent> Emitted() =>
    [
        new AccountDeletionCancelled(Noon, "deletion-cancelled") { Subject = Subject },
        new AccountDeletionRequested(Noon, "deletion-requested", DeletionOrigin.OutOfBandRequest, Noon.AddDays(30))
        {
            Subject = Subject,
            Actor = Actor,
            Effective = Subject,
        },
        new AccountReactivated(Noon, "reactivated") { Subject = Subject },
        new AccountRegistered(Noon, "registered") { Subject = Subject },
        new AccountSuspended(Noon, "suspended", SuspensionOrigin.Administrator) { Subject = Subject, Actor = Actor },
        new AlertRaised(
            Noon,
            "alert",
            AlertCondition.SmsBalance,
            AlertSeverity.High,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["gateway"] = JsonSerializer.SerializeToElement("sms"),
            }),
        new ConsentChanged(Noon, "consent", "newsletter", ConsentChange.Withdrawn) { Subject = Subject },
        new CredentialEnrolled(Noon, "enrolled", new AuthenticatorId(Guid.CreateVersion7(Noon)), Factor.Passkey),
        new CredentialInvalidated(Noon, "invalidated", new AuthenticatorId(Guid.CreateVersion7(Noon)), Factor.Password),
        new CredentialRestored(Noon, "restored", new AuthenticatorId(Guid.CreateVersion7(Noon)), Factor.Passkey),
        new CredentialSuspended(
            Noon,
            "credential-suspended",
            new AuthenticatorId(Guid.CreateVersion7(Noon)),
            Factor.Passkey,
            Noon.AddDays(7)),
        new DeviceVerified(Noon, "device", new DeviceId(Guid.CreateVersion7(Noon))) { Subject = Subject },
        new ErasureRequested(Noon, "erasure", ErasureReason.MinorTakedown) { Subject = Subject },
        new ExportRequested(Noon, "export") { Subject = Subject },
        new IdentifierAdded(Noon, "identifier-added", new IdentifierId(Guid.CreateVersion7(Noon)), IdentifierKind.Phone),
        new IdentifierPrimaryChanged(
            Noon,
            "identifier-primary",
            new IdentifierId(Guid.CreateVersion7(Noon)),
            IdentifierKind.Email),
        new IdentifierRemoved(Noon, "identifier-removed", new IdentifierId(Guid.CreateVersion7(Noon)), IdentifierKind.Email),
        new MembershipChanged(
            Noon,
            "membership",
            new MembershipId(Guid.CreateVersion7(Noon)),
            new OrganizationId(Guid.CreateVersion7(Noon)),
            MembershipChange.Ended),
        new ObjectionChanged(Noon, "objection", "profiling", Objecting: true) { Subject = Subject },
        new OrganizationErased(Noon, "organization-erased", new OrganizationId(Guid.CreateVersion7(Noon)), 3),
        new RestrictionChanged(Noon, "restriction", Restricted: true) { Subject = Subject },
        new SendingRestrictionChanged(Noon, "sending-restriction", "sms-per-number", Loosening: true) { Actor = Actor },
        new SendingRestrictionGranted(Noon, "sending-grant", "sms-per-number", 2, "a support request") { Actor = Actor },
        new TakedownExecuted(Noon, "takedown") { Subject = Subject },
        new TakedownReversed(Noon, "takedown-reversed") { Subject = Subject },
    ];

    private static async Task<IReadOnlyList<PendingEvent>> DueAsync(StoreContext reading, DateTimeOffset now, int count)
    {
        var events = new PendingEvents(reading);
        var due = new List<PendingEvent>();

        foreach (PendingEventId waiting in await events.DueAsync(now, count, TestContext.Current.CancellationToken))
        {
            due.Add(await events.FindAsync(waiting, TestContext.Current.CancellationToken)
                ?? throw new Xunit.Sdk.XunitException("The event was not written."));
        }

        return due;
    }

    private async Task AddedAsync(PendingEvent pending)
    {
        await using StoreContext writing = database.Context();

        await new PendingEvents(writing).AddAsync(pending, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // A claim as a pass takes it: one conditional update, committed on its own.
    private async Task<EventClaim?> ClaimedAsync(PendingEventId pending, DateTimeOffset now)
    {
        await using StoreContext claiming = database.Context();

        return await new PendingEvents(claiming).ClaimAsync(pending, now, Timeout, TestContext.Current.CancellationToken);
    }

    private async Task EmptiedAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        await connection.ExecuteAsync("DELETE FROM identity.events;");
    }
}
