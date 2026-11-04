using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Authentication.Tests.Records;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Records;

/// <summary>
/// A host's own page reading the subjects the browser's record access session reaches,
/// to show its own data about them (REC-ACC-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class RecordReachTests : IAsyncDisposable
{
    private const string Orders = "/shop/orders";

    private const string Address = "guest@example.test";

    private static readonly RandomNumberGenerator Randomness = RandomNumberGenerator.Create();

    private readonly Deployment _deployment;

    private readonly OrganizationId _shop;

    private Result<IReadOnlyList<SubjectId>> _reached;

    /// <summary>
    /// A deployment whose host shows a guest's orders, with an organization it keeps
    /// guests' details in.
    /// </summary>
    public RecordReachTests()
    {
        _deployment = new Deployment(host: Map);
        Flow.Prepare(_deployment);

        _shop = OrganizationId.New(_deployment.Clock);
        _deployment.Organizations.Seed(_shop, name: "Seaside Rentals");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// REC-ACC-003 AC1: a request from the browser whose record access session reaches two
    /// records answers both subjects, in subject order.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_003_AC1_TheSubjectsTheSessionReachesAreAnsweredInOrderAsync()
    {
        SubjectId first = Held(verifiedAgo: TimeSpan.FromDays(1));
        SubjectId second = Held(verifiedAgo: TimeSpan.FromDays(2));

        Browser browser = await OpenedAsync();

        Assert.Equal(new[] { first, second }.OrderBy(subject => subject.Value), await ReachedAsync(browser));
    }

    /// <summary>
    /// REC-ACC-003 AC1: a session that reaches no record, its value verified longer ago than
    /// <c>records.proof.age</c> and no reference given, answers an empty list.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_003_AC1_ASessionReachingNothingAnswersAnEmptyListAsync()
    {
        _ = Held(verifiedAgo: TimeSpan.FromDays(200));

        Browser browser = await OpenedAsync();

        Assert.Empty(await ReachedAsync(browser));
    }

    /// <summary>
    /// REC-ACC-003 AC2: a request from another browser, after
    /// <c>POST /records/access/end</c>, and after <c>records.access.lifetime</c> has passed
    /// is refused with <c>auth.session.expired</c>.
    /// </summary>
    /// <param name="refusal">Which ending is tried.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("another browser")]
    [InlineData("ended")]
    [InlineData("past its lifetime")]
    public async Task REC_ACC_003_AC2_ASessionThatHasEndedIsRefusedAsync(string refusal)
    {
        _ = Held(verifiedAgo: TimeSpan.FromDays(1));

        Browser browser = await OpenedAsync();

        switch (refusal)
        {
            case "another browser":
                browser = new Browser(_deployment);
                _ = await browser.SendAsync("GET", "/records/access");
                break;
            case "ended":
                Assert.Equal(
                    StatusCodes.Status204NoContent,
                    (await browser.SendAsync("POST", "/records/access/end")).Status);
                break;
            case "past its lifetime":
                _deployment.Clock.Advance(TimeSpan.FromMinutes(15));
                break;
        }

        Assert.Equal(StatusCodes.Status204NoContent, (await browser.SendAsync("GET", Orders)).Status);
        Assert.Equal(
            ErrorCodes.SessionExpired,
            _reached.Match(subjects => throw new InvalidOperationException("Reached " + subjects.Count + "."), error => error.Code));
    }

    /// <summary>
    /// REC-ACC-003: a browser signed in reaches its records through its account, so it
    /// holds no record access session and is refused with <c>auth.session.expired</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_003_ABrowserSignedInHoldsNoSessionAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Assert.Equal(StatusCodes.Status204NoContent, (await browser.SendAsync("GET", Orders)).Status);
        Assert.Equal(
            ErrorCodes.SessionExpired,
            _reached.Match(subjects => throw new InvalidOperationException("Reached " + subjects.Count + "."), error => error.Code));
    }

    /// <summary>
    /// REC-ACC-003 AC3: a record attached to an account after the session opened is not
    /// answered.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_003_AC3_ARecordAttachedAfterTheSessionOpenedIsNotAnsweredAsync()
    {
        SubjectId kept = Held(verifiedAgo: TimeSpan.FromDays(1));
        SubjectId attached = Held(verifiedAgo: TimeSpan.FromDays(1));

        Browser browser = await OpenedAsync();

        Assert.Equal(new[] { kept, attached }.OrderBy(subject => subject.Value), await ReachedAsync(browser));

        _deployment.Records.Of(attached).AttachedTo = SubjectId.New(Randomness);

        Assert.Equal([kept], await ReachedAsync(browser));
    }

    /// <summary>
    /// REC-ACC-003 AC4: called while no browser's request is served, the read is a fault of
    /// the calling code.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_003_AC4_OutsideABrowsersRequestTheReadIsAFaultAsync()
    {
        await using AsyncServiceScope scope = _deployment.Scope();

        IRecordAccess access = scope.ServiceProvider.GetRequiredService<IRecordAccess>();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await access.ReachedAsync(CancellationToken.None));
    }

    // A record collected in the shop, holding the address as a contact verified at a
    // collection some time ago.
    private SubjectId Held(TimeSpan verifiedAgo)
    {
        var subject = SubjectId.New(Randomness);
        DateTimeOffset verified = _deployment.Clock.GetUtcNow() - verifiedAgo;

        _deployment.Records.Hold(
            new HeldRecord(subject, _shop, verified)
            {
                Language = "en",
                Contacts = { [IdentifierKind.Email] = new CollectedContact(IdentifierKind.Email, Address, verified) },
            });

        return subject;
    }

    // A browser that asked for a code to the address and opened its session with it.
    private async Task<Browser> OpenedAsync()
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/records/access");

        Assert.Equal(
            StatusCodes.Status202Accepted,
            (await browser.SendAsync("POST", "/records/access", ("kind", "email"), ("value", Address))).Status);

        _ = await _deployment.CarrySendsAsync();

        Assert.Equal(
            StatusCodes.Status200OK,
            (await browser.SendAsync("POST", "/records/access/verify", ("code", Flow.Code(_deployment, IdentifierKind.Email)))).Status);

        return browser;
    }

    private async Task<IReadOnlyList<SubjectId>> ReachedAsync(Browser browser)
    {
        Assert.Equal(StatusCodes.Status204NoContent, (await browser.SendAsync("GET", Orders)).Status);

        return _reached.Match(subjects => subjects, error => throw new InvalidOperationException("Refused: " + error.Code + "."));
    }

    // The host's page of a guest's orders, which reads what the browser reaches.
    private void Map(IEndpointRouteBuilder endpoints) =>
        _ = endpoints.MapGet(Orders, async (IRecordAccess access, CancellationToken cancellationToken) =>
        {
            _reached = await access.ReachedAsync(cancellationToken);

            return Results.NoContent();
        }).AllowNoSession();
}
