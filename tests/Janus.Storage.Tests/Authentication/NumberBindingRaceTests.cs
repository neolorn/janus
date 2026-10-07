using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication;
using Janus.Authentication.Credentials;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.SignIn;
using Janus.Core;
using Janus.Identity.Identifiers;
using Janus.Storage.Authentication.Credentials;
using Janus.Storage.Authentication.Factors;
using Janus.Storage.Authentication.Identifiers;
using Janus.Storage.Authentication.SignIn;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Identity.Preferences;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The races between a number leaving an account and what leans on the text code bound
/// to it, against the database: each request is a transaction of its own that takes its
/// locks in the order the service that makes it does, the identifier's row before the
/// enrolment, the credential or the code's record (AUTH-FACT-002 AC10 and AC11,
/// CONV-DESIGN-003).
/// </summary>
[Trait("kind", "integration")]
public sealed class NumberBindingRaceTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private const string AccountRow = "%identity.accounts%FOR UPDATE%";

    private static readonly DateTimeOffset Noon = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan Window = TimeSpan.FromDays(7);

    private readonly Deployment _deployment = new(database);

    // The tests share one database, so each account holds a number no other does.
    private readonly string _number = "+4416329" + System.Security.Cryptography.RandomNumberGenerator.GetInt32(10000, 100000).ToString(System.Globalization.CultureInfo.InvariantCulture);

    // The account's primary number, which is not the one the credential is bound to: the
    // primary of a kind is not removed.
    private readonly string _primary = "+4416328" + System.Security.Cryptography.RandomNumberGenerator.GetInt32(10000, 100000).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// AUTH-FACT-002 AC10: a confirm that races the number's removal and waits for it
    /// finds, under the locks, the enrolment ended and the identifier gone, so it is
    /// answered as a code that expired and enrols nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002_AC10_AConfirmThatWaitsForTheNumbersRemovalEnrolsNothingAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (SubjectId subject, IdentifierId number) = await AccountAsync();

        await StagedAsync(subject);

        await using StoreContext removing = database.Context();
        await using var removal = new UnitOfWork(removing);
        await using StoreContext confirming = database.Context();
        await using var confirm = new UnitOfWork(confirming);

        await removal.BeginAsync(cancellationToken);
        await TakenAsync(Of(removing), subject, number, cancellationToken);

        await confirm.BeginAsync(cancellationToken);

        Task<bool> confirmed = ConfirmedAsync(Of(confirming), subject, number, cancellationToken);

        await BlockedAsync(AccountRow, cancellationToken);

        Assert.False(confirmed.IsCompleted);

        await removal.CommitAsync(cancellationToken);

        Assert.False(await confirmed.WaitAsync(Bound, cancellationToken));

        await confirm.RollbackAsync();

        Assert.Empty(await CredentialsAsync(subject));
        Assert.Null(await EnrolmentAsync(subject));
    }

    /// <summary>
    /// AUTH-FACT-002 AC10: a removal that races a confirm and waits for it finds, under
    /// the locks, the credential the confirm made, and takes it out of use: it is
    /// suspended and bound to no number when the removal commits.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002_AC10_ARemovalThatWaitsForAConfirmTakesItsCredentialOutOfUseAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (SubjectId subject, IdentifierId number) = await AccountAsync();

        await StagedAsync(subject);

        await using StoreContext removing = database.Context();
        await using var removal = new UnitOfWork(removing);
        await using StoreContext confirming = database.Context();
        await using var confirm = new UnitOfWork(confirming);

        await confirm.BeginAsync(cancellationToken);

        Assert.True(await ConfirmedAsync(Of(confirming), subject, number, cancellationToken));

        await removal.BeginAsync(cancellationToken);

        Task taken = TakenAsync(Of(removing), subject, number, cancellationToken);

        await BlockedAsync(AccountRow, cancellationToken);

        Assert.False(taken.IsCompleted);

        await confirm.CommitAsync(cancellationToken);
        await taken.WaitAsync(Bound, cancellationToken);
        await removal.CommitAsync(cancellationToken);

        Authenticator held = Assert.Single(await CredentialsAsync(subject));

        Assert.Equal(
            (AuthenticatorState.Suspended, true, Noon + Window),
            (held.State, held.IsUnbound, held.InvalidatesAt));
        Assert.Null(await EnrolmentAsync(subject));
    }

    /// <summary>
    /// AUTH-FACT-002 AC11: an ask that read its credential active and waits, at the
    /// account's row, for the writer that is taking the number from the account,
    /// finds under that lock the number gone and the credential suspended, and issues
    /// no code.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002_AC11_AnAskThatWaitsForTheNumberToLeaveIssuesNoCodeAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (SubjectId subject, IdentifierId number) = await AccountAsync();
        Authenticator read = await BoundAsync(subject);

        await using StoreContext removing = database.Context();
        await using var removal = new UnitOfWork(removing);
        await using StoreContext asking = database.Context();
        await using var ask = new UnitOfWork(asking);

        // The ask reads the credential before its unit of work begins.
        Assert.True((await Credentials(asking).FindAsync(read.Id, cancellationToken))?.IsUsable);

        await removal.BeginAsync(cancellationToken);
        await TakenAsync(Of(removing), subject, number, cancellationToken);

        await ask.BeginAsync(cancellationToken);

        Task<ErrorCode?> issued = AskedAsync(Of(asking), read, number, cancellationToken);

        await BlockedAsync(AccountRow, cancellationToken);

        Assert.False(issued.IsCompleted);

        await removal.CommitAsync(cancellationToken);

        Assert.Equal(ErrorCodes.CredentialSuspended, await issued.WaitAsync(Bound, cancellationToken));

        await ask.CommitAsync(cancellationToken);

        Assert.Null(await CodeAsync(subject));
        Assert.Equal(AuthenticatorState.Suspended, Assert.Single(await CredentialsAsync(subject)).State);
    }

    /// <summary>
    /// AUTH-FACT-002 AC11, AUTH-FACT-004 AC7: a writer that takes the number from the
    /// account while an ask is issuing its code waits, at the account's row, for the
    /// ask to commit, and then takes the credential out of use, so the code the ask
    /// issued is one whose credential's number left the account.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002_AC11_AWriterThatWaitsForAnAskTakesTheCredentialOutOfUseAfterItsCodeIsIssuedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (SubjectId subject, IdentifierId number) = await AccountAsync();
        Authenticator read = await BoundAsync(subject);

        await using StoreContext removing = database.Context();
        await using var removal = new UnitOfWork(removing);
        await using StoreContext asking = database.Context();
        await using var ask = new UnitOfWork(asking);

        await ask.BeginAsync(cancellationToken);

        Assert.Null(await AskedAsync(Of(asking), read, number, cancellationToken));

        await removal.BeginAsync(cancellationToken);

        Task taken = TakenAsync(Of(removing), subject, number, cancellationToken);

        await BlockedAsync(AccountRow, cancellationToken);

        Assert.False(taken.IsCompleted);

        await ask.CommitAsync(cancellationToken);
        await taken.WaitAsync(Bound, cancellationToken);
        await removal.CommitAsync(cancellationToken);

        Authenticator held = Assert.Single(await CredentialsAsync(subject));

        Assert.Equal(read.Id, (await CodeAsync(subject))?.Credential);
        Assert.Equal((AuthenticatorState.Suspended, true), (held.State, held.IsUnbound));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    // The writer, as a removal, a replace's swap and an undo that displaces the number
    // each run it: the account's row, then the identifier's row, then the enrolment,
    // then the credentials, and only then the number's leaving. A credential the account
    // would reach less without is suspended for the window and bound to no number.
    private async Task TakenAsync(
        Request request,
        SubjectId subject,
        IdentifierId number,
        CancellationToken cancellationToken)
    {
        IdentifierStore identifiers = request.Identifiers;
        PendingEnrolmentStore enrolments = request.Enrolments;
        AuthenticatorStore credentials = request.Credentials;

        await identifiers.HoldAsync(subject, cancellationToken);
        await identifiers.LockAsync(subject, number, cancellationToken);

        if (await enrolments.FindForUpdateAsync(subject, Factor.PhoneCode, cancellationToken) is { } staged
            && staged.Number == _number)
        {
            await enrolments.RemoveAsync(subject, Factor.PhoneCode, cancellationToken);
        }

        foreach (Authenticator bound in (await credentials.OfForUpdateAsync(subject, cancellationToken))
            .Where(held => held.Number == _number))
        {
            bound.Unbind();
            bound.Suspend(Noon + Window);

            await credentials.RecordAsync(bound, cancellationToken);
        }

        IdentifierSet set = await identifiers.FindBySubjectAsync(subject, cancellationToken);

        await identifiers.RecordRemovalAsync(
            IdentifierRemoval.Of(set.Remove(number), Noon, Noon + TimeSpan.FromDays(1), Guid.NewGuid().ToByteArray()),
            cancellationToken);
        await identifiers.RecordAsync(set, cancellationToken);
    }

    // A confirm, as the credential service runs it: the account's row, then the
    // identifier's row, under which it judges that the identifier still holds the
    // number, verified, then the enrolment, then the credentials. What it answers is
    // whether it enrolled the credential; otherwise the code is one that expired.
    private async Task<bool> ConfirmedAsync(
        Request request,
        SubjectId subject,
        IdentifierId number,
        CancellationToken cancellationToken)
    {
        PendingEnrolmentStore enrolments = request.Enrolments;
        AuthenticatorStore credentials = request.Credentials;

        _ = await request.Accounts.HoldAsync(subject, cancellationToken);

        HeldIdentifier? held = await request.Directory.LockAsync(subject, number, cancellationToken);
        PendingEnrolment? staged = await enrolments.FindForUpdateAsync(subject, Factor.PhoneCode, cancellationToken);

        if (held is not { IsVerified: true } || held.Canonical != _number || staged?.Number != _number)
        {
            return false;
        }

        await enrolments.RemoveAsync(subject, Factor.PhoneCode, cancellationToken);

        _ = await credentials.OfForUpdateAsync(subject, cancellationToken);

        await credentials.AddAsync(
            Authenticator.Bound(
                AuthenticatorId.New(TimeProvider.System),
                subject,
                Factor.PhoneCode,
                staged.Label,
                staged.Number,
                Noon),
            cancellationToken);

        return true;
    }

    // An ask, as the sign-in runs it: the account's row, which the record of its code
    // refers to, then the identifier's row, then the credential under its own lock, and
    // the code's record only where the credential is still active and the number still
    // the account's. What it answers is what the ask is refused with where the
    // credential is now suspended, and nothing where the code was issued.
    private async Task<ErrorCode?> AskedAsync(
        Request request,
        Authenticator read,
        IdentifierId number,
        CancellationToken cancellationToken)
    {
        _ = await request.Accounts.HoldAsync(read.Subject, cancellationToken);

        HeldIdentifier? locked = await request.Directory.LockAsync(read.Subject, number, cancellationToken);
        Authenticator? standing = await request.Credentials.FindForUpdateAsync(read.Id, cancellationToken);

        if (standing is not { IsUsable: true }
            || standing.Number != _number
            || locked is not { IsPending: false }
            || locked.Canonical != _number)
        {
            return standing is { IsAwaitingInvalidation: true }
                ? ErrorCodes.CredentialSuspended
                : ErrorCodes.FactorRejected;
        }

        await request.Codes.ReplaceAsync(
            PendingSignIn.Issue(
                OpaqueToken.Draw(_deployment.Randomness),
                read.Subject,
                Factor.PhoneCode,
                email: null,
                read.Id,
                "481516",
                browser: null,
                challenge: System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray()),
                Noon,
                TimeSpan.FromMinutes(10)),
            cancellationToken);

        return null;
    }

    // An account that holds one number, verified.
    private async Task<(SubjectId Subject, IdentifierId Number)> AccountAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var id = IdentifierId.New(TimeProvider.System);

        Assert.True(PhoneNumber.TryParse(_number, out PhoneNumber number));
        Assert.True(PhoneNumber.TryParse(_primary, out PhoneNumber primary));

        await using StoreContext context = database.Context();
        IdentifierStore store = Identifiers(context);
        IdentifierSet set = await store.FindBySubjectAsync(subject, TestContext.Current.CancellationToken);

        var first = IdentifierId.New(TimeProvider.System);

        set.Add(Identifier.Phone(first, subject, primary, _primary, Noon), maximum: 5);
        set.Verify(first, Noon);
        set.Add(Identifier.Phone(id, subject, number, _number, Noon), maximum: 5);
        set.Verify(id, Noon);

        await store.RecordAsync(set, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (subject, id);
    }

    private async Task StagedAsync(SubjectId subject)
    {
        await using StoreContext context = database.Context();

        await Enrolments(context).ReplaceAsync(
            PendingEnrolment.Stage(
                OpaqueToken.Draw(_deployment.Randomness),
                subject,
                Factor.PhoneCode,
                Label(),
                _number,
                "162342",
                Noon,
                TimeSpan.FromMinutes(10)),
            TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Authenticator> BoundAsync(SubjectId subject)
    {
        var bound = Authenticator.Bound(
            AuthenticatorId.New(TimeProvider.System),
            subject,
            Factor.PhoneCode,
            Label(),
            _number,
            Noon);

        await using StoreContext context = database.Context();

        await Credentials(context).AddAsync(bound, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return bound;
    }

    private async Task<IReadOnlyList<Authenticator>> CredentialsAsync(SubjectId subject)
    {
        await using StoreContext context = database.Context();

        return await Credentials(context).OfAsync(subject, TestContext.Current.CancellationToken);
    }

    private async Task<PendingEnrolment?> EnrolmentAsync(SubjectId subject)
    {
        await using StoreContext context = database.Context();

        return await Enrolments(context).FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken);
    }

    private async Task<PendingSignIn?> CodeAsync(SubjectId subject)
    {
        await using StoreContext context = database.Context();

        return await Codes(context).FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken);
    }

    // The other request is waiting on the row lock this one holds, as the database
    // itself reports it, so the case goes on only once the wait is real.
    private async Task BlockedAsync(string waiting, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(Bound);

        while (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                   "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() "
                       + "AND wait_event_type = 'Lock' AND query LIKE @waiting",
                   new { waiting },
                   cancellationToken: bounded.Token)) == 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), bounded.Token);
        }
    }

    private static CredentialLabel Label() =>
        CredentialLabel.TryParse("My phone", out CredentialLabel label)
            ? label
            : throw new Xunit.Sdk.XunitException("The label is one the chapter admits.");

    private Request Of(StoreContext context) =>
        new(
            new AccountStore(context),
            Identifiers(context),
            Directory(context),
            Enrolments(context),
            Credentials(context),
            Codes(context));

    private IdentifierStore Identifiers(StoreContext context) =>
        new(context, _deployment.Ring, _deployment.Randomness);

    private IdentifierDirectory Directory(StoreContext context) =>
        new(
            Identifiers(context),
            new PreferenceStore(context, _deployment.Ring, _deployment.Randomness),
            new PendingVerificationStore(context, _deployment.Ring, _deployment.Randomness));

    private PendingEnrolmentStore Enrolments(StoreContext context) =>
        new(context, _deployment.Ring, _deployment.Randomness);

    private AuthenticatorStore Credentials(StoreContext context) =>
        new(context, _deployment.Ring, _deployment.Randomness);

    private PendingSignInStore Codes(StoreContext context) =>
        new(context, _deployment.Ring, _deployment.Randomness, new DataConnections(context));

    // The stores of one request, each over that request's own connection.
    private sealed record Request(
        AccountStore Accounts,
        IdentifierStore Identifiers,
        IdentifierDirectory Directory,
        PendingEnrolmentStore Enrolments,
        AuthenticatorStore Credentials,
        PendingSignInStore Codes);
}
