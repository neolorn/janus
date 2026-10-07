using System;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Credentials;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Authentication.Credentials;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The enrolments a texted code has yet to confirm as the <c>pending_enrolments</c>
/// rows carry them (AUTH-FACT-002, AUTH-FACT-004).
/// </summary>
[Trait("kind", "integration")]
public sealed class PendingEnrolmentStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private const string Number = "+441632960031";

    private const string Code = "481516";

    private static readonly DateTimeOffset Noon = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// AUTH-FACT-002 AC9: a pending enrolment reads back as it was staged, and its
    /// number and its code are in the table under the person's key and not in plain.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002_AC9_APendingEnrolmentReadsBackAndItsNumberAndCodeAreNotInTheTableInPlainAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        PendingEnrolment staged = Staged(subject, "my phone", Number, Code, Noon);

        await ReplacedAsync(staged);

        await using StoreContext reading = database.Context();
        PendingEnrolmentRecord stored = await reading.PendingEnrolments
            .SingleAsync(held => held.Subject == subject, TestContext.Current.CancellationToken);
        PendingEnrolment read = Assert.IsType<PendingEnrolment>(
            await Store(reading).FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));

        Assert.Equal(PersonalDataFormat.Marker, stored.Number[0]);
        Assert.Equal(PersonalDataFormat.Marker, stored.Code[0]);
        Assert.Equal(-1, stored.Number.AsSpan().IndexOf(Encoding.UTF8.GetBytes(Number)));
        Assert.Equal(-1, stored.Code.AsSpan().IndexOf(Encoding.UTF8.GetBytes(Code)));
        Assert.Equal(-1, stored.Code.AsSpan().IndexOf(VerificationCode.Held(Code)));
        Assert.Equal(
            (Number, "my phone", Noon, Noon + Lifetime, 0),
            (read.Number, read.Label.Value, read.IssuedAt, read.ExpiresAt, read.WrongAttempts));
        Assert.Equal(staged.Send, read.Send);
        Assert.True(read.Matches(Code));
    }

    /// <summary>
    /// AUTH-FACT-002: staging again writes over what the account had pending, so one
    /// row stands and the code of the earlier enrolment confirms nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002_AC9_StagingAgainReplacesThePendingEnrolmentAsync()
    {
        const string later = "+441632960032";

        SubjectId subject = await _deployment.AccountAsync(Noon);

        await ReplacedAsync(Staged(subject, "my phone", Number, Code, Noon));
        await ReplacedAsync(Staged(subject, "the other phone", later, "162342", Noon + TimeSpan.FromMinutes(1)));

        await using StoreContext reading = database.Context();
        PendingEnrolment read = Assert.IsType<PendingEnrolment>(
            await Store(reading).FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));

        Assert.Equal(
            1,
            await reading.PendingEnrolments.CountAsync(held => held.Subject == subject, TestContext.Current.CancellationToken));
        Assert.Equal((later, "the other phone"), (read.Number, read.Label.Value));
        Assert.False(read.Matches(Code));
        Assert.True(read.Matches("162342"));
    }

    /// <summary>
    /// AUTH-FACT-004: a wrong code is counted on the row, so the count survives the
    /// request that typed it; a removed enrolment is gone.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_004_AWrongCodeIsCountedOnTheRowAndARemovedEnrolmentIsGoneAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await ReplacedAsync(Staged(subject, "my phone", Number, Code, Noon));

        await using (StoreContext counting = database.Context())
        await using (var work = new UnitOfWork(counting))
        {
            Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));

            PendingEnrolment held = Assert.IsType<PendingEnrolment>(await Store(counting)
                .FindForUpdateAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));

            held.Missed();

            await Store(counting).RecordAsync(held, TestContext.Current.CancellationToken);

            Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));
        }

        await using (StoreContext reading = database.Context())
        {
            PendingEnrolment read = Assert.IsType<PendingEnrolment>(
                await Store(reading).FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));

            Assert.Equal(1, read.WrongAttempts);

            await Store(reading).RemoveAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken);
            await reading.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext after = database.Context();

        Assert.Null(await Store(after).FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-FACT-002 AC10: a pending enrolment is held only inside the operation's
    /// transaction, so a caller with none open is refused the lock and not handed a
    /// row another request may be ending.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002_AC10_APendingEnrolmentIsLockedOnlyInsideATransactionAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await ReplacedAsync(Staged(subject, "my phone", Number, Code, Noon));

        await using StoreContext context = database.Context();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Store(context)
            .FindForUpdateAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-FACT-002: the sweep removes an enrolment whose code stopped working and
    /// leaves one whose code still works.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002_TheSweepRemovesAnEnrolmentPastItsLifetimeAndNoOtherAsync()
    {
        SubjectId lapsed = await _deployment.AccountAsync(Noon);
        SubjectId standing = await _deployment.AccountAsync(Noon);

        await ReplacedAsync(Staged(lapsed, "my phone", Number, Code, Noon));
        await ReplacedAsync(Staged(standing, "my phone", Number, Code, Noon + TimeSpan.FromMinutes(1)));

        await using (StoreContext sweeping = database.Context())
        {
            Assert.True(await Store(sweeping).SweepAsync(Noon + Lifetime, TestContext.Current.CancellationToken) >= 1);
        }

        await using StoreContext reading = database.Context();

        Assert.Null(await Store(reading).FindAsync(lapsed, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.NotNull(await Store(reading).FindAsync(standing, Factor.PhoneCode, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static CredentialLabel Label(string entered) =>
        CredentialLabel.TryParse(entered, out CredentialLabel label)
            ? label
            : throw new Xunit.Sdk.XunitException("The label is one the chapter admits.");

    private PendingEnrolment Staged(SubjectId subject, string label, string number, string code, DateTimeOffset at) =>
        PendingEnrolment.Stage(
            OpaqueToken.Draw(_deployment.Randomness),
            subject,
            Factor.PhoneCode,
            Label(label),
            number,
            code,
            at,
            Lifetime);

    private async Task ReplacedAsync(PendingEnrolment pending)
    {
        await using StoreContext writing = database.Context();

        await Store(writing).ReplaceAsync(pending, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private PendingEnrolmentStore Store(StoreContext context) =>
        new(context, _deployment.Ring, _deployment.Randomness);
}
