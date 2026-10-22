using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Tests;
using Janus.Core;
using Janus.Hosting.Privacy;
using Janus.Privacy.Consents;
using Janus.Privacy.Tests.Documents;
using Xunit;

namespace Janus.Hosting.Tests.Privacy;

/// <summary>
/// The comparison of the cookies a deployment sets with those the current version of the
/// privacy notice lists, and the degradation it raises under <c>notice.cookies</c>
/// (PRIV-CONS-006a, chapter 10 section 5.23).
/// </summary>
[Trait("kind", "unit")]
public sealed class NoticeCookieCheckTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly NoticeCookie Session = new("__Host-identity-session", null, TimeSpan.FromDays(365), false);

    private static readonly NoticeCookie Device = new("__Host-identity-device", null, TimeSpan.FromDays(30), false);

    private static readonly NoticeCookie Picks = new("host-picks", "recommendations", TimeSpan.FromDays(90), true);

    private readonly LegalDocumentStoreInMemory _documents = new();

    private readonly DeploymentCookiesInMemory _cookies = new();

    private readonly EventsInMemory _alerts = new();

    private NoticeCookieCheck Check => new(_documents, _cookies, _alerts, new FixedClock(Noon));

    /// <summary>
    /// PRIV-CONS-006a AC4 (D-203): a deployment with no version of the notice published
    /// yet raises nothing, whatever cookies it sets, and does not read them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_006a_AC4_NothingIsRaisedBeforeAFirstVersionIsPublishedAsync()
    {
        _cookies.Set = [Device, Session];

        Result compared = await Check.ComparedAsync(TestContext.Current.CancellationToken);

        Assert.True(compared.Match(() => true, _ => false));
        Assert.Empty(_alerts.Of<AlertRaised>());
        Assert.Equal(0, _cookies.Reads);
    }

    /// <summary>
    /// PRIV-CONS-006a AC4 (D-203): cookies that are those the current version lists, in
    /// any order, raise nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_006a_AC4_CookiesTheCurrentVersionListsRaiseNothingAsync()
    {
        Published([Session, Picks, Device]);
        _cookies.Set = [Device, Picks, Session];

        Result compared = await Check.ComparedAsync(TestContext.Current.CancellationToken);

        Assert.True(compared.Match(() => true, _ => false));
        Assert.Empty(_alerts.Of<AlertRaised>());
    }

    /// <summary>
    /// PRIV-CONS-006a AC4, chapter 10 section 5.23 (D-203): cookies that differ from the
    /// current version's list raise <c>degradation</c> under <c>notice.cookies</c>, its
    /// details naming each cookie set and not listed, listed and not set, and listed
    /// differently, by name.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_006a_AC4_CookiesThatDifferRaiseADegradationNamingThemAsync()
    {
        Published([Device, Session]);
        _cookies.Set = [Device with { Lifetime = TimeSpan.FromDays(14) }, Picks];

        Result compared = await Check.ComparedAsync(TestContext.Current.CancellationToken);

        Assert.True(compared.Match(() => true, _ => false));
        AlertRaised raised = Assert.Single(_alerts.Of<AlertRaised>());
        Assert.Equal(AlertCondition.Degradation, raised.Condition);
        Assert.Equal("notice.cookies", raised.Scope);
        Assert.Equal(Noon, raised.RaisedAt);
        Assert.Equal(["host-picks"], Names(raised, "added"));
        Assert.Equal(["__Host-identity-session"], Names(raised, "removed"));
        Assert.Equal(["__Host-identity-device"], Names(raised, "changed"));
    }

    /// <summary>
    /// PRIV-CONS-006a AC4 (D-203): a cookie differs by its purpose and by its readability
    /// by a third party as much as by its lifetime.
    /// </summary>
    /// <param name="purpose">The purpose the cookie is set for now.</param>
    /// <param name="thirdParty">Whether a third party reads it now.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("analytics", true)]
    [InlineData("recommendations", false)]
    public async Task PRIV_CONS_006a_AC4_APurposeOrAThirdPartyReadingThatDiffersIsAChangeAsync(
        string purpose,
        bool thirdParty)
    {
        Published([Picks]);
        _cookies.Set = [Picks with { Purpose = purpose, ThirdParty = thirdParty }];

        _ = await Check.ComparedAsync(TestContext.Current.CancellationToken);

        AlertRaised raised = Assert.Single(_alerts.Of<AlertRaised>());
        Assert.Empty(Names(raised, "added"));
        Assert.Empty(Names(raised, "removed"));
        Assert.Equal(["host-picks"], Names(raised, "changed"));
    }

    /// <summary>
    /// PRIV-CONS-006a (D-203): where the cookies the deployment sets cannot be read, the
    /// comparison answers the failure and raises nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_006a_ACookieListThatCannotBeReadAnswersTheFailureAsync()
    {
        Published([Session]);
        _cookies.Failure = Error.From(ErrorCodes.SystemFault);

        Result compared = await Check.ComparedAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.SystemFault, compared.Match<ErrorCode?>(() => null, error => error.Code));
        Assert.Empty(_alerts.Of<AlertRaised>());
    }

    private void Published(IReadOnlyList<NoticeCookie> cookies) =>
        _documents.Hold(new DocumentVersion(ConsentService.Notice, "1", "ar", "النص", [], Noon.AddDays(-1))
        {
            Cookies = cookies,
        });

    private static string[] Names(AlertRaised raised, string detail) =>
        [.. raised.Details[detail].EnumerateArray().Select(name => name.GetString()!)];
}
