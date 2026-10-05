using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Storage.Authentication.Factors;
using Janus.Storage.Authentication.Identifiers;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Identity.Preferences;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The verifications a live account has outstanding, as the database keeps them
/// (REG-IDENT-004, REG-IDENT-007).
/// </summary>
/// <remarks>The port implementations are tested against the real database (D-156).</remarks>
[Trait("kind", "integration")]
public sealed class PendingVerificationStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// REG-IDENT-004, CONV-DESIGN-003 AC6: a value proved by several requests at once
    /// is decided each time on the verification's row under its lock, so the first
    /// proves it and every other finds it proved. The wrong tries of its code are
    /// counted on the verification-code record (AUTH-FACT-004 AC4).
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_004_AValueProvedAtOnceIsProvedOnceAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var staged = IdentifierId.New(TimeProvider.System);
        var identity = StagedIdentity.Of(staged, IdentifierKind.Email, "person@example.test", "person@example.test");

        identity.Linked(RandomNumberGenerator.GetBytes(Fingerprint.Length));

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).AddAsync(
                PendingVerification.ToAdd(subject, browser: null, identity, Noon),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        bool[] proved = await Task.WhenAll(ProvedAsync(staged), ProvedAsync(staged), ProvedAsync(staged));

        await using StoreContext reading = database.Context();
        PendingVerification read = Assert.IsType<PendingVerification>(
            await Store(reading).FindAsync(staged, TestContext.Current.CancellationToken));

        Assert.Single(proved, by => by);
        Assert.Equal(Noon, read.Staged.VerifiedAt);
    }

    /// <summary>
    /// REG-IDENT-004 AC5: a pending add writes no identifier. The account lists it,
    /// after its identifiers, as an unverified identifier under the pending
    /// verification's identifier, where it counts toward its kind; a replace staged on
    /// the account is not listed.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_004_AC5_APendingAddIsListedUnverifiedAndWritesNoIdentifierAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        string held = Fresh("held");
        string adding = Fresh("adding");
        string replacing = Fresh("replacing");
        var identifier = IdentifierId.New(TimeProvider.System);
        var add = IdentifierId.New(TimeProvider.System);

        await using (StoreContext writing = database.Context())
        {
            await Directory(writing).TakeOnAsync(
                subject,
                identifier,
                IdentifierKind.Email,
                held,
                held,
                Noon,
                TestContext.Current.CancellationToken);
            await Store(writing).AddAsync(
                PendingVerification.ToAdd(
                    subject,
                    browser: null,
                    StagedIdentity.Of(add, IdentifierKind.Email, adding, adding),
                    Noon.AddMinutes(1)),
                TestContext.Current.CancellationToken);
            await Store(writing).AddAsync(
                PendingVerification.ToReplace(
                    subject,
                    browser: null,
                    enrolment: null,
                    StagedIdentity.Of(identifier, IdentifierKind.Email, replacing, replacing),
                    oldMustConfirm: false,
                    Noon.AddMinutes(2)),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        HeldIdentifiers listed = await Directory(reading).HeldAsync(subject, TestContext.Current.CancellationToken);

        Assert.Equal([identifier, add], listed.All.Select(one => one.Id));
        Assert.Equal(2, listed.OfKind(IdentifierKind.Email).Count);

        HeldIdentifier pending = listed.All[1];

        Assert.True(pending.IsPending);
        Assert.False(pending.IsVerified);
        Assert.False(pending.IsPrimary);
        Assert.Equal(adding, pending.Canonical);
        Assert.DoesNotContain(listed.NoticeSet, one => one.Id == add);
        Assert.False(await reading.Identifiers.AnyAsync(row => row.Id == add, TestContext.Current.CancellationToken));
        Assert.Null(await Directory(reading).OwnerAsync(
            IdentifierKind.Email,
            adding,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-004 AC5: an add that verifies writes its identifier in the transaction
    /// that ends its pending verification, verified, under the identifier the pending
    /// verification was held under, and the account then lists it once.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_004_AC5_AVerifiedAddIsWrittenUnderItsPendingVerificationsIdentifierAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        string adding = Fresh("verified");
        var add = IdentifierId.New(TimeProvider.System);

        await using (StoreContext staging = database.Context())
        {
            await Store(staging).AddAsync(
                PendingVerification.ToAdd(
                    subject,
                    browser: null,
                    StagedIdentity.Of(add, IdentifierKind.Email, adding, adding),
                    Noon),
                TestContext.Current.CancellationToken);
            await staging.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (StoreContext verifying = database.Context())
        await using (var work = new UnitOfWork(verifying))
        {
            PendingVerificationStore store = Store(verifying);
            IdentifierDirectory directory = Directory(verifying);

            Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));

            await directory.HoldAsync(subject, TestContext.Current.CancellationToken);

            PendingVerification held = Assert.IsType<PendingVerification>(
                await store.FindForUpdateAsync(add, TestContext.Current.CancellationToken));

            await directory.LockValuesAsync(
                [(held.Staged.Kind, held.Staged.Canonical)],
                TestContext.Current.CancellationToken);

            Assert.Null(await directory.OwnerAsync(
                held.Staged.Kind,
                held.Staged.Canonical,
                TestContext.Current.CancellationToken));

            await directory.TakeOnAsync(
                subject,
                held.Identifier,
                held.Staged.Kind,
                held.Staged.Entered,
                held.Staged.Canonical,
                Noon.AddMinutes(5),
                TestContext.Current.CancellationToken);
            await store.RemoveAsync(add, TestContext.Current.CancellationToken);

            Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));
        }

        await using StoreContext reading = database.Context();
        HeldIdentifier written = Assert.Single(
            (await Directory(reading).HeldAsync(subject, TestContext.Current.CancellationToken)).All);

        Assert.Equal(add, written.Id);
        Assert.True(written.IsVerified);
        Assert.True(written.IsPrimary);
        Assert.False(written.IsPending);
        Assert.Equal(Noon.AddMinutes(5), written.VerifiedAt);
        Assert.Null(await Store(reading).FindAsync(add, TestContext.Current.CancellationToken));
        Assert.Equal(subject, await Directory(reading).OwnerAsync(
            IdentifierKind.Email,
            adding,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-004 AC4, REG-IDENT-007 AC4 and AC6 (D-166, 306), AUTH-FACT-004: the
    /// sweep keeps a verification whose code still stands, and a replace whose
    /// confirmation alone still stands, and removes one whose every record is spent or
    /// past its lifetime, a record at the instant of its expiry included. The holders
    /// the statement computes are the ones the records were written under.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_004_AVerificationWhoseCodeStillStandsSurvivesTheSweepAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var lifetime = TimeSpan.FromMinutes(10);
        DateTimeOffset now = Noon.AddMinutes(5);

        IdentifierId standing = await StagedAsync(subject, replacing: false);
        IdentifierId lapsed = await StagedAsync(subject, replacing: false);
        IdentifierId lapsing = await StagedAsync(subject, replacing: false);
        IdentifierId spent = await StagedAsync(subject, replacing: false);
        IdentifierId confirming = await StagedAsync(subject, replacing: true);
        IdentifierId unconfirmed = await StagedAsync(subject, replacing: true);

        await using (StoreContext writing = database.Context())
        {
            var codes = new VerificationCodeStore(writing, new DataConnections(writing));

            foreach (VerificationCode code in new[]
            {
                VerificationCode.Issue(PendingVerification.CodeHolder(standing), "123456", Noon, lifetime),
                VerificationCode.Issue(PendingVerification.CodeHolder(lapsed), "123456", Noon - lifetime, lifetime),
                VerificationCode.Issue(PendingVerification.CodeHolder(lapsing), "123456", now - lifetime, lifetime),
                VerificationCode.Unanswerable(PendingVerification.ConfirmationHolder(confirming), Noon, lifetime),
                VerificationCode.Unanswerable(PendingVerification.ConfirmationHolder(unconfirmed), Noon - lifetime, lifetime),
            })
            {
                await codes.AddAsync(code, TestContext.Current.CancellationToken);
            }

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.True(await SweptAsync(now) >= 4);

        await using StoreContext reading = database.Context();
        PendingVerificationStore store = Store(reading);

        Assert.NotNull(await store.FindAsync(standing, TestContext.Current.CancellationToken));
        Assert.NotNull(await store.FindAsync(confirming, TestContext.Current.CancellationToken));
        Assert.Null(await store.FindAsync(lapsed, TestContext.Current.CancellationToken));
        Assert.Null(await store.FindAsync(lapsing, TestContext.Current.CancellationToken));
        Assert.Null(await store.FindAsync(spent, TestContext.Current.CancellationToken));
        Assert.Null(await store.FindAsync(unconfirmed, TestContext.Current.CancellationToken));
        Assert.Equal(
            standing,
            Assert.Single(await store.AddsOfAsync(subject, TestContext.Current.CancellationToken)).Identifier);
    }

    /// <summary>
    /// REG-IDENT-004 (D-188), OPS-OBS-003: a resend holds the pending verification's row
    /// while it writes, and the sweep passes over a candidate another transaction holds
    /// without waiting for it, so the resend in flight keeps its record: the
    /// verification whose every record had lapsed stands once the resend has written its
    /// new code, and the sweep that ran meanwhile ended the lapsed one nobody held.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_004_TheSweepPassesOverAVerificationAResendHoldsAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var lifetime = TimeSpan.FromMinutes(10);
        DateTimeOffset now = Noon.AddMinutes(30);

        IdentifierId resent = await StagedAsync(subject, replacing: false);
        IdentifierId lapsed = await StagedAsync(subject, replacing: false);

        await using (StoreContext resending = database.Context())
        await using (var resend = new UnitOfWork(resending))
        {
            Assert.True((await resend.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));
            Assert.NotNull(await Store(resending).FindForUpdateAsync(resent, TestContext.Current.CancellationToken));

            _ = await SweptAsync(now);

            await new VerificationCodeStore(resending, new DataConnections(resending)).AddAsync(
                VerificationCode.Issue(PendingVerification.CodeHolder(resent), "123456", now, lifetime),
                TestContext.Current.CancellationToken);

            Assert.True((await resend.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));
        }

        _ = await SweptAsync(now);

        await using StoreContext reading = database.Context();

        Assert.NotNull(await Store(reading).FindAsync(resent, TestContext.Current.CancellationToken));
        Assert.Null(await Store(reading).FindAsync(lapsed, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-007 AC3 (D-189): a replace keeps the enrolment session that staged it,
    /// and one a session staged keeps none.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_007_AC3_AReplaceKeepsTheEnrolmentSessionThatStagedItAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var opened = EnrolmentSessionId.New(TimeProvider.System);
        var staged = IdentifierId.New(TimeProvider.System);
        string value = Fresh("enrolled");
        IdentifierId elsewhere = await StagedAsync(subject, replacing: true);

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).AddAsync(
                PendingVerification.ToReplace(
                    subject,
                    browser: null,
                    opened,
                    StagedIdentity.Of(staged, IdentifierKind.Email, value, value),
                    oldMustConfirm: false,
                    Noon),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Equal(
            opened,
            (await Store(reading).FindAsync(staged, TestContext.Current.CancellationToken))?.Enrolment);
        Assert.Null((await Store(reading).FindAsync(elsewhere, TestContext.Current.CancellationToken))?.Enrolment);
    }

    /// <summary>
    /// REG-IDENT-007 AC9 (D-190): a replace staged afresh is carried onto the row of the
    /// staging before it: the session it is now staged for, whether the displaced
    /// address must confirm, when it was staged, and neither link the earlier staging
    /// had sent.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_007_AC9_AReplaceStagedAfreshIsCarriedOntoTheRowOfTheStagingBeforeItAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var opened = EnrolmentSessionId.New(TimeProvider.System);
        var staged = IdentifierId.New(TimeProvider.System);
        string value = Fresh("afresh");
        var before = PendingVerification.ToReplace(
            subject,
            browser: null,
            enrolment: null,
            StagedIdentity.Of(staged, IdentifierKind.Email, value, value),
            oldMustConfirm: true,
            Noon);
        byte[] link = RandomNumberGenerator.GetBytes(Fingerprint.Length);
        before.AskedOld(RandomNumberGenerator.GetBytes(Fingerprint.Length));
        before.Staged.Linked(link);

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).AddAsync(before, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (StoreContext restaging = database.Context())
        {
            await Store(restaging).RecordAsync(
                PendingVerification.ToReplace(
                    subject,
                    browser: null,
                    opened,
                    StagedIdentity.Of(staged, IdentifierKind.Email, value, value),
                    oldMustConfirm: false,
                    Noon.AddMinutes(5)),
                TestContext.Current.CancellationToken);
            await restaging.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        PendingVerification? afresh = await Store(reading).FindAsync(staged, TestContext.Current.CancellationToken);

        Assert.NotNull(afresh);
        Assert.Equal((opened, false, Noon.AddMinutes(5)), (afresh.Enrolment, afresh.OldMustConfirm, afresh.StagedAt));
        Assert.Null(afresh.OldLink);
        Assert.Null(await Store(reading).FindByLinkAsync(link, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-004 (D-188): the sweep locks its candidates, which it can do only inside
    /// a transaction, so one asked for outside any is a fault and deletes nothing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_004_TheSweepRunsOnlyInsideATransactionAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        IdentifierId lapsed = await StagedAsync(subject, replacing: false);

        await using StoreContext sweeping = database.Context();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Store(sweeping).SweepAsync(Noon, TestContext.Current.CancellationToken).AsTask());

        Assert.NotNull(await Store(sweeping).FindAsync(lapsed, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static string Fresh(string person) =>
        person + "." + Guid.NewGuid().ToString("N") + "@example.test";

    // A verification staged at noon, of an add or of a replace whose old address must
    // confirm, with no record written for it.
    private async Task<IdentifierId> StagedAsync(SubjectId subject, bool replacing)
    {
        var staged = IdentifierId.New(TimeProvider.System);
        string value = Fresh("staged");
        var identity = StagedIdentity.Of(staged, IdentifierKind.Email, value, value);

        await using StoreContext writing = database.Context();

        await Store(writing).AddAsync(
            replacing
                ? PendingVerification.ToReplace(subject, browser: null, enrolment: null, identity, oldMustConfirm: true, Noon)
                : PendingVerification.ToAdd(subject, browser: null, identity, Noon),
            TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return staged;
    }

    // One pass of the sweep in a transaction of its own, as the expiry sweep runs it.
    private async Task<int> SweptAsync(DateTimeOffset now)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));

        int ended = await Store(context).SweepAsync(now, TestContext.Current.CancellationToken);

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return ended;
    }

    // Each proof is its own request, read before its transaction and again under the
    // lock, as the identifier service does, and says whether it was the one that
    // proved the value.
    private async Task<bool> ProvedAsync(IdentifierId staged)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);
        PendingVerificationStore store = Store(context);

        _ = await store.FindAsync(staged, TestContext.Current.CancellationToken);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));

        PendingVerification held = Assert.IsType<PendingVerification>(
            await store.FindForUpdateAsync(staged, TestContext.Current.CancellationToken));

        bool proving = !held.Staged.IsVerified;

        if (proving)
        {
            held.Staged.Verify(Noon);

            await store.RecordAsync(held, TestContext.Current.CancellationToken);
        }

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return proving;
    }

    private PendingVerificationStore Store(StoreContext context) =>
        new(context, _deployment.Ring, _deployment.Randomness);

    private IdentifierDirectory Directory(StoreContext context) =>
        new(
            new IdentifierStore(context, _deployment.Ring, _deployment.Randomness),
            new PreferenceStore(context, _deployment.Ring, _deployment.Randomness),
            Store(context));
}
