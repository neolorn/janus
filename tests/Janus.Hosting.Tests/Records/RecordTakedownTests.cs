using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Authentication.Sending;
using Janus.Authentication.Tests.Records;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Outbox;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Records;

/// <summary>
/// Staff taking a person record down, reading the takedown and reversing it
/// (REC-TKD-001, chapter 09 section 8a).
/// </summary>
[Trait("kind", "unit")]
public sealed class RecordTakedownTests : IAsyncDisposable
{
    private const string Address = "guest@example.test";

    private const string Language = "en";

    private static readonly RandomNumberGenerator Randomness = RandomNumberGenerator.Create();

    private readonly Deployment _deployment = new();

    private readonly OrganizationId _shop;

    /// <summary>
    /// A deployment whose record access messages carry the code alone, with an
    /// organization a host keeps guests' details in.
    /// </summary>
    public RecordTakedownTests()
    {
        Flow.Prepare(_deployment);

        _shop = OrganizationId.New(_deployment.Clock);
        _deployment.Organizations.Seed(_shop, name: "Seaside Rentals");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// REC-TKD-001 AC1: a takedown of an active record takes it into <c>deleting</c> by
    /// <c>takedown</c>, ends the record access session reaching it, writes
    /// <c>TakedownExecuted</c> and no <c>AccountSuspended</c>, and no code reaches the
    /// record afterwards.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_TKD_001_AC1_ATakedownEndsTheRecordsAccessAndNoCodeReachesItAsync()
    {
        SubjectId record = Holding();
        _ = await OpenedAsync();
        Browser staff = await TakingDownAsync();

        Answer taken = await staff.SendAsync(
            "POST", Takedown(record), ("trigger", "staff-report"), ("reason", "seen with a school card"));

        HeldRecord held = _deployment.Records.Of(record);
        Assert.Equal(StatusCodes.Status202Accepted, taken.Status);
        Assert.Equal(
            _deployment.Clock.GetUtcNow() + Settings.TakedownGrace.Default,
            taken.Json().GetProperty("erasureDue").GetDateTimeOffset());
        Assert.Equal(
            (PersonRecordState.Deleting, (DeletionOrigin?)DeletionOrigin.Takedown),
            (held.State, held.DeletingBy));
        Assert.Empty(_deployment.RecordAccess.All);
        Assert.Contains(
            _deployment.Outbox.Deliveries,
            delivery => delivery.Subject == record && delivery.Kind is SubjectEventKind.TakedownExecuted);
        Assert.Empty(_deployment.Events.Of<AccountSuspended>());

        _deployment.Clock.Advance(TimeSpan.FromMinutes(2));
        _ = await AskedAsync(new Browser(_deployment));
        _ = await _deployment.CarrySendsAsync();

        Assert.Equal(MessageKind.RecordAccessNone, _deployment.Mail.Taken[^1].Kind);
    }

    /// <summary>
    /// REC-TKD-001: the takedown is read back as an account's is; a second is refused
    /// <c>identity.takedown.active</c>, one of an erased record
    /// <c>identity.record.stateconflict</c>, and one behind a stale proof asks the staff
    /// member to step up, taking nothing down.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_TKD_001_ATakedownIsReadAndASecondErasedOrUnprovedOneIsRefusedAsync()
    {
        SubjectId record = Holding();
        SubjectId erased = Holding();
        SubjectId later = Holding();
        _deployment.Records.Of(erased).State = PersonRecordState.Deleted;
        _deployment.AccountStates.HoldRecord(erased, AccountState.Deleted);
        Browser staff = await TakingDownAsync();

        Answer taken = await TakeDownAsync(staff, record);
        Answer read = await staff.SendAsync("GET", Takedown(record));
        Answer unread = await staff.SendAsync("GET", Takedown(later));
        Answer again = await TakeDownAsync(staff, record);
        Answer gone = await TakeDownAsync(staff, erased);

        _deployment.Clock.Advance(Settings.SessionStepUpRecency.Default + TimeSpan.FromMinutes(1));

        Answer stale = await TakeDownAsync(staff, later);

        Assert.Equal(
            (StatusCodes.Status200OK, taken.Json().GetProperty("takedownId").GetGuid()),
            (read.Status, read.Json().GetProperty("takedownId").GetGuid()));
        Assert.Equal(
            (StatusCodes.Status404NotFound, ErrorCodes.TakedownNotFound.ToString()),
            (unread.Status, unread.Text("code")));
        Assert.Equal(
            (StatusCodes.Status409Conflict, ErrorCodes.TakedownActive.ToString()),
            (again.Status, again.Text("code")));
        Assert.Equal(
            (StatusCodes.Status409Conflict, ErrorCodes.RecordStateConflict.ToString(), "deleted"),
            (gone.Status, gone.Text("code"), gone.Json().GetProperty("details").GetProperty("state").GetString()));
        Assert.Equal(
            (StatusCodes.Status403Forbidden, ErrorCodes.StepUpRequired.ToString(), PersonRecordState.Active),
            (stale.Status, stale.Text("code"), _deployment.Records.Of(later).State));
    }

    /// <summary>
    /// REC-TKD-001 AC2: reversed inside its window, a record returns to what it held at
    /// the trigger, restricted here, and once the window has closed the reversal is
    /// refused <c>identity.takedown.windowelapsed</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_TKD_001_AC2_AReversedRecordReturnsToWhatItHeldAsync()
    {
        SubjectId record = Holding();
        SubjectId late = Holding();
        _deployment.Records.Of(record).State = PersonRecordState.Restricted;
        _deployment.AccountStates.HoldRecord(record, AccountState.Restricted);
        Browser staff = await TakingDownAsync();
        _ = await TakeDownAsync(staff, record);
        _ = await TakeDownAsync(staff, late);

        Answer reversed = await staff.SendAsync("POST", Takedown(record) + "/reverse", ("reason", "an adult, misjudged"));

        _deployment.Clock.Advance(Settings.TakedownGrace.Default);

        Answer elapsed = await staff.SendAsync("POST", Takedown(late) + "/reverse", ("reason", "too late"));

        HeldRecord held = _deployment.Records.Of(record);
        Assert.Equal(
            (StatusCodes.Status204NoContent, PersonRecordState.Restricted, (DeletionOrigin?)null),
            (reversed.Status, held.State, held.DeletingBy));
        Assert.Equal(
            (StatusCodes.Status422UnprocessableEntity, ErrorCodes.TakedownWindowElapsed.ToString(), PersonRecordState.Deleting),
            (elapsed.Status, elapsed.Text("code"), _deployment.Records.Of(late).State));
        Assert.Equal(record, Assert.Single(_deployment.Events.Of<TakedownReversed>()).Subject);
    }

    /// <summary>
    /// REC-TKD-001 AC3: a takedown naming a record attached to an account is refused
    /// <c>identity.record.followsaccount</c> naming the account, and one naming a subject
    /// no record bears <c>identity.record.notfound</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_TKD_001_AC3_AnAttachedRecordFollowsItsAccountAsync()
    {
        var account = SubjectId.New(Randomness);
        SubjectId attached = Holding();
        _deployment.Records.Of(attached).AttachedTo = account;
        _deployment.Records.Of(attached).AttachedAt = _deployment.Clock.GetUtcNow();
        _deployment.AccountStates.HoldRecord(attached, AccountState.Active, attachedTo: account);
        Browser staff = await TakingDownAsync();

        Answer followed = await TakeDownAsync(staff, attached);
        Answer missing = await TakeDownAsync(staff, SubjectId.New(Randomness));

        Assert.Equal(
            (StatusCodes.Status409Conflict, ErrorCodes.RecordFollowsAccount.ToString(), account.Value),
            (followed.Status, followed.Text("code"), followed.Json().GetProperty("details").GetProperty("account").GetGuid()));
        Assert.Equal(
            (StatusCodes.Status404NotFound, ErrorCodes.RecordNotFound.ToString()),
            (missing.Status, missing.Text("code")));
        Assert.Equal(PersonRecordState.Active, _deployment.Records.Of(attached).State);
        Assert.Empty(_deployment.Events.Of<AccountSuspended>());
    }

    /// <summary>
    /// REC-TKD-001: a caller holding <c>takedown:execute</c> in no organization that
    /// could admit it is answered <c>identity.record.notfound</c> in the bytes a subject
    /// no record bears is answered with, and nothing is taken down.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_TKD_001_ACallerAdmittedNowhereIsAnsweredAsForNoRecordAsync()
    {
        SubjectId record = Holding();
        Browser stranger = await Flow.SignedInAsync(_deployment);

        Answer refused = await TakeDownAsync(stranger, record);
        Answer missing = await TakeDownAsync(stranger, SubjectId.New(Randomness));

        Assert.Equal((StatusCodes.Status404NotFound, Without(missing)), (refused.Status, Without(refused)));
        Assert.Equal(PersonRecordState.Active, _deployment.Records.Of(record).State);
    }

    // The bytes of an answer but its correlation identifier, which each request has its own of.
    private static string Without(Answer answer) =>
        answer.Body.Replace(answer.Text("correlationId"), string.Empty, StringComparison.Ordinal);

    private static string Takedown(SubjectId record) => $"/admin/records/{record.Value}/takedown";

    private static Task<Answer> TakeDownAsync(Browser browser, SubjectId record) =>
        browser.SendAsync("POST", Takedown(record), ("trigger", "customer-report"), ("reason", "a parent wrote in"));

    private static async Task<Answer> AskedAsync(Browser browser)
    {
        _ = await browser.SendAsync("GET", "/records/access");

        return await browser.SendAsync("POST", "/records/access", ("kind", "email"), ("value", Address));
    }

    // A signed-in staff member holding takedown:execute in the organization.
    private async Task<Browser> TakingDownAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, _shop, Permissions.TakedownExecute);

        return browser;
    }

    // A browser that asked for a code to the record's email and opened its session.
    private async Task<Browser> OpenedAsync()
    {
        var browser = new Browser(_deployment);

        Assert.Equal(StatusCodes.Status202Accepted, (await AskedAsync(browser)).Status);

        _ = await _deployment.CarrySendsAsync();

        Answer opened = await browser.SendAsync("POST", "/records/access/verify", ("code", Flow.Code(_deployment, IdentifierKind.Email)));

        Assert.Equal(StatusCodes.Status200OK, opened.Status);

        return browser;
    }

    // A record collected in the organization, holding an email verified a day ago,
    // which the privacy area holds as a person record.
    private SubjectId Holding()
    {
        DateTimeOffset verified = _deployment.Clock.GetUtcNow() - TimeSpan.FromDays(1);
        var record = new HeldRecord(SubjectId.New(Randomness), _shop, verified)
        {
            Language = Language,
            Contacts = { [IdentifierKind.Email] = new CollectedContact(IdentifierKind.Email, Address, verified) },
        };

        _deployment.Records.Hold(record);
        _deployment.AccountStates.HoldRecord(record.Subject, AccountState.Active);

        return record.Subject;
    }
}
