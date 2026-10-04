using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Recovery;
using Janus.Core;
using Janus.Storage.Authentication.Recovery;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The links recovery sends, as the <c>recovery_links</c> table holds them: spent once,
/// whatever reaches them at once (AUTH-RECOV-002, AUTH-RECOV-005).
/// </summary>
[Trait("kind", "integration")]
public sealed class RecoveryLinkStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// AUTH-RECOV-002 AC1, CONV-DESIGN-003 AC6: two requests opening one enrolment link
    /// at once read it under its lock, so the second waits for the first to commit,
    /// finds it spent, and one session is opened.
    /// </summary>
    [Fact]
    public async Task AUTH_RECOV_002_AC1_TwoOpeningsOfOneLinkAtOnceOpenOnceAsync()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SubjectId subject = await _deployment.AccountAsync(now);
        var token = OpaqueToken.Draw(_deployment.Randomness);

        await using (StoreContext issuing = database.Context())
        {
            await new RecoveryLinkStore(issuing).ReplaceAsync(
                RecoveryLink.Issue(token, subject, RecoveryPurpose.Enrolment, now, TimeSpan.FromHours(1)),
                TestContext.Current.CancellationToken);
            await issuing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        bool[] opened = await Task.WhenAll(OpenedAsync(token, now), OpenedAsync(token, now));

        Assert.Equal(1, opened.Count(answer => answer));
    }

    /// <summary>
    /// AUTH-RECOV-002 AC1, D-148, CONV-DESIGN-003 AC6: two completions under one
    /// enrolment session at once hold the session under its link's lock, so the second
    /// waits for the first to end it and finds it ended.
    /// </summary>
    [Fact]
    public async Task AUTH_RECOV_002_AC1_TwoCompletionsOfOneEnrolmentSessionAtOnceCompleteOnceAsync()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SubjectId subject = await _deployment.AccountAsync(now);
        var link = RecoveryLink.Issue(
            OpaqueToken.Draw(_deployment.Randomness),
            subject,
            RecoveryPurpose.Enrolment,
            now,
            TimeSpan.FromHours(1));
        var opened = EnrolmentSessionId.New(TimeProvider.System);

        link.Spend(opened, now);

        await using (StoreContext issuing = database.Context())
        {
            await new RecoveryLinkStore(issuing).ReplaceAsync(link, TestContext.Current.CancellationToken);
            await issuing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        bool[] completed = await Task.WhenAll(CompletedAsync(opened), CompletedAsync(opened));

        Assert.Equal(1, completed.Count(answer => answer));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    // Each completion is its own request, holding the session and ending it as the
    // credential service does around the credential it writes.
    private async Task<bool> CompletedAsync(EnrolmentSessionId opened)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);
        var enrolments = new EnrolmentSessions(new RecoveryLinkStore(context), work, TimeProvider.System);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));

        bool held = await enrolments.HoldAsync(opened, TestContext.Current.CancellationToken);

        if (held)
        {
            await enrolments.EndAsync(opened, TestContext.Current.CancellationToken);
        }

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return held;
    }

    // Each opening is its own request: its own context, connection and transaction,
    // deciding on the link as the recovery service does.
    private async Task<bool> OpenedAsync(OpaqueToken token, DateTimeOffset now)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);
        var links = new RecoveryLinkStore(context);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));

        RecoveryLink? link = await links.FindForUpdateAsync(
            token.Fingerprint(),
            TestContext.Current.CancellationToken);
        bool opens = link is not null && link.Opens(RecoveryPurpose.Enrolment, now);

        if (opens)
        {
            link!.Spend(EnrolmentSessionId.New(TimeProvider.System), now);

            await links.RecordAsync(link, TestContext.Current.CancellationToken);
        }

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return opens;
    }
}
