using System;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Erasures;
using Janus.Privacy.Outbox;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Privacy;

/// <summary>
/// The erasure endpoints of chapter 09 section 8a: every outstanding erasure, one
/// erasure's per-subscriber state, and the manual completion of one whose retries were
/// spent (IDN-LIFE-003a, IDN-LIFE-003b).
/// </summary>
[Trait("kind", "unit")]
public sealed class ErasureEndpointTests : IAsyncDisposable
{
    private const string Path = "/admin/erasures";

    private static readonly OrganizationId Company =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser.
    /// </summary>
    public ErasureEndpointTests() => Flow.Prepare(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// 09 section 8a: the listing and the read answer 200 with the erasure's identifier,
    /// subject, reason, status, attempts and subscribers, and an identifier naming no
    /// erasure is 404 <c>privacy.erasure.notfound</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003b_TheOutstandingErasuresAreListedAndReadAsync()
    {
        Delivery delivery = await FailedAsync();
        Browser browser = await AuthorisedAsync();

        Answer listed = await browser.SendAsync("GET", Path);
        Answer read = await browser.SendAsync("GET", $"{Path}/{delivery.Id.Value}");
        Answer unknown = await browser.SendAsync("GET", $"{Path}/{Guid.NewGuid()}");

        JsonElement erasure = listed.Json()[0];
        JsonElement one = read.Json();

        Assert.Equal(StatusCodes.Status200OK, listed.Status);
        Assert.Equal(1, listed.Json().GetArrayLength());
        Assert.Equal(delivery.Id.Value, erasure.GetProperty("id").GetGuid());
        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Equal(delivery.Id.Value, one.GetProperty("id").GetGuid());
        Assert.Equal(Ahmed.Value, one.GetProperty("subject").GetGuid());
        Assert.Equal("minor-takedown", one.GetProperty("reason").GetString());
        Assert.Equal("failed", one.GetProperty("status").GetString());
        Assert.Equal("erasure-ledger", one.GetProperty("subscribers")[0].GetProperty("name").GetString());
        Assert.True(one.GetProperty("subscribers")[0].GetProperty("required").GetBoolean());
        Assert.Equal(StatusCodes.Status404NotFound, unknown.Status);
        Assert.Equal(ErrorCodes.ErasureNotFound.ToString(), unknown.Text("code"));
    }

    /// <summary>
    /// IDN-LIFE-003a: the manual completion of a failed erasure is answered 204 and
    /// closes it, and a second is 409 <c>privacy.erasure.notfailed</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003a_AFailedErasureIsCompletedByHandAsync()
    {
        Delivery delivery = await FailedAsync();
        Browser browser = await AuthorisedAsync();

        Answer completed = await browser.SendAsync("POST", $"{Path}/{delivery.Id.Value}/complete");
        Answer again = await browser.SendAsync("POST", $"{Path}/{delivery.Id.Value}/complete");

        Assert.Equal(StatusCodes.Status204NoContent, completed.Status);
        Assert.Equal(ErasureStatus.Complete, delivery.Status);
        Assert.Single(_deployment.Ledger.Lines);
        Assert.Equal(ErasureStatus.Complete, Assert.Single(_deployment.Erasures.Erasures).Status);
        Assert.Equal(StatusCodes.Status409Conflict, again.Status);
        Assert.Equal(ErrorCodes.ErasureNotFailed.ToString(), again.Text("code"));
        Assert.Equal(0, (await browser.SendAsync("GET", Path)).Json().GetArrayLength());
    }

    /// <summary>
    /// DR-016 AC2: while the ledger cannot take the line the manual completion is a
    /// fault, answered 500 <c>system.fault</c> with nothing else, and the erasure stays
    /// failed until a completion finds the line durable.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_016_AC2_AManualCompletionIsAFaultWhileTheLedgerCannotTakeTheLineAsync()
    {
        Delivery delivery = await FailedAsync();
        Browser browser = await AuthorisedAsync();

        _deployment.Ledger.Durable = false;

        Answer refused = await browser.SendAsync("POST", $"{Path}/{delivery.Id.Value}/complete");

        Assert.Equal(StatusCodes.Status500InternalServerError, refused.Status);
        Assert.Equal(ErrorCodes.SystemFault.ToString(), refused.Text("code"));
        Assert.Empty(refused.Json().GetProperty("details").EnumerateObject());
        Assert.Equal(ErasureStatus.Failed, delivery.Status);

        _deployment.Ledger.Durable = true;

        Answer completed = await browser.SendAsync("POST", $"{Path}/{delivery.Id.Value}/complete");

        Assert.Equal(StatusCodes.Status204NoContent, completed.Status);
        Assert.Equal(ErasureStatus.Complete, delivery.Status);
    }

    /// <summary>
    /// AUTHZ-CONCEAL-005: a customer holding nothing but their own session is refused
    /// every one of the three, and nothing is closed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003b_TheEndpointsAreRefusedToACustomerAsync()
    {
        Delivery delivery = await FailedAsync();
        Browser browser = await Flow.SignedInAsync(_deployment);

        Assert.Equal(StatusCodes.Status403Forbidden, (await browser.SendAsync("GET", Path)).Status);
        Assert.Equal(
            StatusCodes.Status403Forbidden,
            (await browser.SendAsync("GET", $"{Path}/{delivery.Id.Value}")).Status);
        Assert.Equal(
            StatusCodes.Status403Forbidden,
            (await browser.SendAsync("POST", $"{Path}/{delivery.Id.Value}/complete")).Status);
        Assert.Equal(ErasureStatus.Failed, delivery.Status);
    }

    private async Task<Browser> AuthorisedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        _deployment.Administers(Company);
        _deployment.Gate.Grant(subject, Company, Permissions.PrivacyRequestManage);

        return browser;
    }

    // An erasure whose retries were spent: its delivery and its row, failed together.
    private async Task<Delivery> FailedAsync()
    {
        DateTimeOffset at = _deployment.Clock.GetUtcNow();
        var delivery = Delivery.Of(Ahmed, SubjectEventKind.ErasureRequested, at, reason: ErasureReason.MinorTakedown);
        var row = Erasure.Begun(Ahmed, at, ErasureReason.MinorTakedown);

        delivery.Fail();
        row.Fail();

        await _deployment.Outbox.AddAsync(delivery, TestContext.Current.CancellationToken);
        _deployment.Erasures.Add(row);

        return delivery;
    }
}
