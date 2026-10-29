using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Authentication.Tests.Records;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Records;

/// <summary>
/// The collection session as a browser drives it: the age first where it is asked,
/// each contact and its code, and the browser it is bound to (REC-COL-001,
/// REC-AGE-001, BFF-CSRF-005b).
/// </summary>
[Trait("kind", "unit")]
public sealed class CollectionEndpointsTests : IAsyncDisposable
{
    private const string Number = "+441632960011";

    private static readonly RandomNumberGenerator Randomness = RandomNumberGenerator.Create();

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment whose messages carry the code, as the shipped templates do.
    /// </summary>
    public CollectionEndpointsTests() => Flow.Prepare(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// REC-COL-001 AC1: with the age asked, no contact is taken before it is answered,
    /// and one is taken after.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_001_AC1_NoContactIsTakenBeforeTheAgeIsAnsweredAsync()
    {
        Browser browser = await BegunAsync(age: true);

        Answer early = await browser.SendAsync("PUT", "/records/collection/phone", ("value", Number));

        Assert.Equal(StatusCodes.Status409Conflict, early.Status);
        Assert.Equal(ErrorCodes.CollectionIncomplete.ToString(), early.Text("code"));
        Assert.Empty(_deployment.Sms.Taken);

        Assert.Equal(
            StatusCodes.Status204NoContent,
            (await browser.SendAsync("PUT", "/records/collection/age", ("dateOfBirth", "1990-01-01"))).Status);
        Assert.Equal(
            StatusCodes.Status202Accepted,
            (await browser.SendAsync("PUT", "/records/collection/phone", ("value", Number))).Status);
    }

    /// <summary>
    /// REC-COL-001 AC1, REC-AGE-001 AC2: on an adults-only deployment an under-age date
    /// ends the session, and the same session takes no further date and no contact.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_001_AC1_AnUnderAgeDateEndsTheSessionAndTakesNothingFurtherAsync()
    {
        Browser browser = await BegunAsync(age: true);
        string young = DateOnly.FromDateTime(_deployment.Clock.GetUtcNow().UtcDateTime)
            .AddYears(-10)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        Answer refused = await browser.SendAsync("PUT", "/records/collection/age", ("dateOfBirth", young));
        Answer again = await browser.SendAsync("PUT", "/records/collection/age", ("dateOfBirth", "1990-01-01"));
        Answer contact = await browser.SendAsync("PUT", "/records/collection/phone", ("value", Number));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.Status);
        Assert.Equal(ErrorCodes.ProfileUnderage.ToString(), refused.Text("code"));
        Assert.Equal(ErrorCodes.ProfileUnderage.ToString(), again.Text("code"));
        Assert.Equal(ErrorCodes.CollectionIncomplete.ToString(), contact.Text("code"));
        Assert.Empty(_deployment.Sms.Taken);
    }

    /// <summary>
    /// REC-AGE-001 AC2: an under-age answer at collection ends the session, and no
    /// contact is taken from it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_AGE_001_AC2_AnUnderAgeAnswerTakesNoContactAsync()
    {
        Browser browser = await BegunAsync(age: true);
        string young = DateOnly.FromDateTime(_deployment.Clock.GetUtcNow().UtcDateTime)
            .AddYears(-17)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        _ = await browser.SendAsync("PUT", "/records/collection/age", ("dateOfBirth", young));

        Answer contact = await browser.SendAsync("PUT", "/records/collection/email", ("value", Flow.Address));

        Assert.Equal(StatusCodes.Status409Conflict, contact.Status);
        Assert.Empty(_deployment.Mail.Taken);
        Assert.Equal("asked", (await browser.SendAsync("GET", "/records/collection")).Text("age"));
    }

    /// <summary>
    /// REC-COL-001: an age the session does not ask, or already holds, is refused as out
    /// of order.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_001_AnAgeTheSessionDoesNotAskOrHoldsIsOutOfOrderAsync()
    {
        Browser unasked = await BegunAsync(age: false);
        Browser answered = await BegunAsync(age: true);

        _ = await answered.SendAsync("PUT", "/records/collection/age", ("dateOfBirth", "1990-01-01"));

        Answer notAsked = await unasked.SendAsync("PUT", "/records/collection/age", ("dateOfBirth", "1990-01-01"));
        Answer held = await answered.SendAsync("PUT", "/records/collection/age", ("dateOfBirth", "1991-01-01"));

        Assert.Equal(ErrorCodes.CollectionIncomplete.ToString(), notAsked.Text("code"));
        Assert.Equal(ErrorCodes.CollectionIncomplete.ToString(), held.Text("code"));
        Assert.Equal("none", (await unasked.SendAsync("GET", "/records/collection")).Text("age"));
        Assert.Equal("answered", (await answered.SendAsync("GET", "/records/collection")).Text("age"));
    }

    /// <summary>
    /// REC-COL-001 AC2: a contact's code verifies only through the browser that began
    /// the session; another browser presenting it is refused as one holding no session.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_001_AC2_ACodeVerifiesOnlyThroughTheBrowserThatBeganAsync()
    {
        Browser began = await BegunAsync(age: false);
        Browser other = await BegunAsync(age: false);

        _ = await began.SendAsync("PUT", "/records/collection/phone", ("value", Number));

        string code = Flow.Code(_deployment, IdentifierKind.Phone);

        Answer elsewhere = await other.SendAsync(
            "POST",
            "/records/collection/verify",
            ("kind", "phone"),
            ("code", code));
        Answer fresh = await new Browser(_deployment).SendAsync(
            "POST",
            "/records/collection/verify",
            ("kind", "phone"),
            ("code", code));
        Answer verified = await began.SendAsync(
            "POST",
            "/records/collection/verify",
            ("kind", "phone"),
            ("code", code));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, elsewhere.Status);
        Assert.Equal(ErrorCodes.CodeExpired.ToString(), elsewhere.Text("code"));
        Assert.NotEqual(StatusCodes.Status204NoContent, fresh.Status);
        Assert.Equal(StatusCodes.Status204NoContent, verified.Status);

        Answer state = await began.SendAsync("GET", "/records/collection");

        Assert.True(Assert.Single(state.Json().GetProperty("contacts").EnumerateArray()).GetProperty("verified").GetBoolean());
    }

    /// <summary>
    /// REC-COL-001 AC3: staging a value an account holds, a value a record holds and a
    /// value no one holds are answered in identical bytes; the session asks no one who
    /// holds a value.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_001_AC3_StagingAHeldValueAnswersAsAFreshOneAsync()
    {
        const string heldNumber = "+441632960022";
        const string recordedNumber = "+441632960033";

        _ = _deployment.Identifiers.Verified(SubjectId.New(Randomness), IdentifierKind.Phone, heldNumber);
        _deployment.Records.Hold(
            new HeldRecord(SubjectId.New(Randomness), OrganizationId.New(_deployment.Clock), _deployment.Clock.GetUtcNow())
            {
                Contacts = { [IdentifierKind.Phone] = new CollectedContact(IdentifierKind.Phone, recordedNumber, _deployment.Clock.GetUtcNow()) },
            });

        Answer unheld = await (await BegunAsync(age: false))
            .SendAsync("PUT", "/records/collection/phone", ("value", Number));
        Answer held = await (await BegunAsync(age: false))
            .SendAsync("PUT", "/records/collection/phone", ("value", heldNumber));
        Answer recorded = await (await BegunAsync(age: false))
            .SendAsync("PUT", "/records/collection/phone", ("value", recordedNumber));

        Assert.Equal(StatusCodes.Status202Accepted, unheld.Status);
        Assert.Equal((unheld.Status, unheld.Body), (held.Status, held.Body));
        Assert.Equal((unheld.Status, unheld.Body), (recorded.Status, recorded.Body));
        Assert.Equal(3, _deployment.Sms.Taken.Count);
    }

    /// <summary>
    /// REC-COL-001 AC4: begun from a browser holding a live sign-in session, nothing is
    /// staged and the request is refused with <c>identity.record.signedin</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_001_AC4_ABrowserSignedInIsRefusedAndStagesNothingAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer refused = await browser.SendAsync("POST", "/records/collection", ("age", false));

        Assert.Equal(StatusCodes.Status409Conflict, refused.Status);
        Assert.Equal(ErrorCodes.RecordSignedIn.ToString(), refused.Text("code"));
        Assert.Empty(_deployment.Collections.All);
    }

    /// <summary>
    /// REC-COL-001 AC4: every step of a collection refuses a browser holding a live
    /// sign-in session as its beginning does (<c>09</c> section 7a).
    /// </summary>
    /// <param name="method">The step's method.</param>
    /// <param name="path">The step's path.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("GET", "/records/collection")]
    [InlineData("PUT", "/records/collection/age")]
    [InlineData("PUT", "/records/collection/email")]
    [InlineData("PUT", "/records/collection/phone")]
    [InlineData("POST", "/records/collection/verify")]
    public async Task REC_COL_001_AC4_EveryStepRefusesABrowserSignedInAsync(string method, string path)
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        int sent = _deployment.Sms.Taken.Count;

        Answer refused = await browser.SendAsync(
            method,
            path,
            ("dateOfBirth", "1990-01-01"),
            ("value", Number),
            ("kind", "phone"),
            ("code", "000000"));

        Assert.Equal(StatusCodes.Status409Conflict, refused.Status);
        Assert.Equal(ErrorCodes.RecordSignedIn.ToString(), refused.Text("code"));
        Assert.Empty(_deployment.Collections.All);
        Assert.Equal(sent, _deployment.Sms.Taken.Count);
    }

    /// <summary>
    /// REC-COL-001 AC5: an abandoned session leaves nothing behind, and the browser that
    /// held it holds no session after it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_001_AC5_AnAbandonedSessionLeavesNothingBehindAsync()
    {
        Browser browser = await BegunAsync(age: false);

        _ = await browser.SendAsync("PUT", "/records/collection/phone", ("value", Number));

        string code = Flow.Code(_deployment, IdentifierKind.Phone);

        Assert.Equal(
            StatusCodes.Status204NoContent,
            (await browser.SendAsync("POST", "/records/collection/abandon")).Status);
        Assert.Empty(_deployment.Collections.All);

        Answer state = await browser.SendAsync("GET", "/records/collection");
        Answer verified = await browser.SendAsync(
            "POST",
            "/records/collection/verify",
            ("kind", "phone"),
            ("code", code));

        Assert.Equal(StatusCodes.Status401Unauthorized, state.Status);
        Assert.Equal(ErrorCodes.SessionExpired.ToString(), verified.Text("code"));
        Assert.Equal(
            StatusCodes.Status204NoContent,
            (await browser.SendAsync("POST", "/records/collection/abandon")).Status);
    }

    /// <summary>
    /// REC-COL-001 AC5: a session past its lifetime answers as one that has ended, and
    /// the sweep removes it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_001_AC5_AnExpiredSessionAnswersAsEndedAndIsSweptAsync()
    {
        Browser browser = await BegunAsync(age: false);

        _deployment.Clock.Advance(TimeSpan.FromHours(1));

        Answer state = await browser.SendAsync("GET", "/records/collection");

        Assert.Equal(ErrorCodes.SessionExpired.ToString(), state.Text("code"));
        Assert.Equal(
            1,
            await _deployment.Collections.SweepAsync(_deployment.Clock.GetUtcNow(), TestContext.Current.CancellationToken));
        Assert.Empty(_deployment.Collections.All);
    }

    /// <summary>
    /// REC-COL-001, AUTH-FACT-004: a wrong code is refused as invalid, a contact staged
    /// again replaces the one before it and its code, and a kind the session holds no
    /// contact of is answered as a lapsed code.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_001_ACodeIsJudgedAgainstTheContactStagedLastAsync()
    {
        Browser browser = await BegunAsync(age: false);

        _ = await browser.SendAsync("PUT", "/records/collection/phone", ("value", "+441632960033"));

        string first = Flow.Code(_deployment, IdentifierKind.Phone);

        _ = await browser.SendAsync("PUT", "/records/collection/phone", ("value", Number));

        string second = Flow.Code(_deployment, IdentifierKind.Phone);
        string wrong = string.Equals(first, "000000", StringComparison.Ordinal) ? "111111" : "000000";

        Answer missed = await browser.SendAsync("POST", "/records/collection/verify", ("kind", "phone"), ("code", wrong));
        Answer unstaged = await browser.SendAsync("POST", "/records/collection/verify", ("kind", "email"), ("code", second));
        Answer verified = await browser.SendAsync("POST", "/records/collection/verify", ("kind", "phone"), ("code", second));

        Assert.Equal(ErrorCodes.CodeInvalid.ToString(), missed.Text("code"));
        Assert.Equal(ErrorCodes.CodeExpired.ToString(), unstaged.Text("code"));
        Assert.Equal(StatusCodes.Status204NoContent, verified.Status);

        Answer state = await browser.SendAsync("GET", "/records/collection");

        Assert.Equal(
            Number,
            Assert.Single(state.Json().GetProperty("contacts").EnumerateArray()).GetProperty("value").GetString());
    }

    /// <summary>
    /// REC-COL-001: a value that is not a well-formed phone is refused, nothing staged
    /// and nothing sent.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_001_AMalformedContactIsRefusedAsync()
    {
        Browser browser = await BegunAsync(age: false);

        Answer refused = await browser.SendAsync("PUT", "/records/collection/phone", ("value", "not a number"));

        Assert.Equal(ErrorCodes.IdentifierInvalid.ToString(), refused.Text("code"));
        Assert.Empty(Assert.Single(_deployment.Collections.All).Contacts);
        Assert.Empty(_deployment.Sms.Taken);
    }

    /// <summary>
    /// Chapter 09 section 7a: <c>age</c> is required when a session is created.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_COL_001_ASessionIsNotCreatedWithoutSayingWhetherItAsksTheAgeAsync()
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/records/collection");

        Answer refused = await browser.SendAsync("POST", "/records/collection", ("other", true));

        Assert.Equal(StatusCodes.Status400BadRequest, refused.Status);
        Assert.Empty(_deployment.Collections.All);
    }

    private async Task<Browser> BegunAsync(bool age)
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/records/collection");

        Answer begun = await browser.SendAsync("POST", "/records/collection", ("age", age));

        Assert.Equal(StatusCodes.Status201Created, begun.Status);
        Assert.Equal(age ? "asked" : "none", begun.Text("age"));
        Assert.Empty(begun.Json().GetProperty("contacts").EnumerateArray().ToArray());

        return browser;
    }
}
