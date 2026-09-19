using System.Collections.Generic;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// What the deployment's WebAuthn configuration has to hold together for it to start,
/// and what it settles (AUTH-FACT-010, AUTH-FACT-011, AUTH-FACT-012).
/// </summary>
[Trait("kind", "unit")]
public sealed class RelyingPartyTests
{
    private static readonly IReadOnlyList<int> Algorithms = [-8, -7, -257];

    /// <summary>
    /// AUTH-FACT-010 AC1: an identifier no configured origin sits under stops the
    /// deployment with the code the chapter names.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_AC1_AnIdentifierOverNoConfiguredOriginIsRefused() =>
        Assert.Equal(
            ErrorCodes.StartupRelyingPartyId,
            Refusal("other.example", ["https://app.example.com", "https://id.example.com"]));

    /// <summary>
    /// AUTH-FACT-010 AC1: an identifier over one origin and not the next is refused
    /// on the one it misses.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_AC1_AnIdentifierOverOnlySomeOriginsIsRefused() =>
        Assert.Equal(
            ErrorCodes.StartupRelyingPartyId,
            Refusal("app.example.com", ["https://app.example.com", "https://id.example.com"]));

    /// <summary>
    /// AUTH-FACT-010 AC1: a label with no registrable parent, which is a public
    /// suffix as often as not, is refused rather than taken as the widest door.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_AC1_AnIdentifierOfOneLabelIsRefused() =>
        Assert.Equal(
            ErrorCodes.StartupRelyingPartyId,
            Refusal("com", ["https://app.example.com"]));

    /// <summary>
    /// AUTH-FACT-010 AC1: a configured origin that is not an origin is refused with
    /// the same code, the check it fails being the one that reads it.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_AC1_AnOriginThatIsNotAnOriginIsRefused() =>
        Assert.Equal(
            ErrorCodes.StartupRelyingPartyId,
            Refusal("example.com", ["example.com"]));

    /// <summary>
    /// AUTH-FACT-010 AC2: with origins across subdomains and no explicit setting, the
    /// derived value is the common parent and not the first origin.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_AC2_TheDerivedIdentifierIsTheCommonParent() =>
        Assert.Equal(
            "example.com",
            Settled(
                string.Empty,
                ["https://app.example.com", "https://id.example.com", "https://example.com"]).Id);

    /// <summary>
    /// AUTH-FACT-010 AC2: the parent is derived at a label boundary, so origins that
    /// share the end of a label and not the label share nothing.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_AC2_TheDerivedIdentifierFollowsLabelBoundaries() =>
        Assert.Equal(
            ErrorCodes.StartupRelyingPartyId,
            Refusal(string.Empty, ["https://app.example.com", "https://notexample.com"]));

    /// <summary>
    /// AUTH-FACT-010: an identifier every origin sits under settles, and what it
    /// settles is what was configured.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_AnIdentifierOverEveryOriginSettles()
    {
        RelyingParty party = Settled(
            "example.com",
            ["https://app.example.com", "https://id.example.com:8443"]);

        Assert.Equal("example.com", party.Id);
        Assert.Equal(Algorithms, party.Algorithms);
    }

    /// <summary>
    /// AUTH-FACT-011: a credential carrying the identifier in force still stands, and
    /// one carrying another does not.
    /// </summary>
    [Fact]
    public void AUTH_FACT_011_AC1_ACredentialUnderAPreviousIdentifierIsDetected()
    {
        RelyingParty party = Settled("example.com", ["https://app.example.com"]);

        Assert.True(party.Binds("example.com"));
        Assert.False(party.Binds("example.net"));
    }

    /// <summary>
    /// AUTH-FACT-012 AC1: the well-known document lists exactly the configured
    /// related origins.
    /// </summary>
    [Fact]
    public void AUTH_FACT_012_AC1_TheDocumentListsExactlyTheConfiguredOrigins() =>
        Assert.Equal(
            """{"origins":["https://example.net","https://example.org"]}""",
            RelyingParty.Of(
                    "example.com",
                    ["https://app.example.com"],
                    ["https://example.net", "https://example.org"],
                    Algorithms)
                .Allowlist());

    /// <summary>
    /// AUTH-FACT-012 AC2: five distinct labels stand, the relying party's own among
    /// them.
    /// </summary>
    [Fact]
    public void AUTH_FACT_012_AC2_FiveDistinctLabelsStand() =>
        Assert.Equal(
            "example.com",
            RelyingParty.Of(
                    "example.com",
                    ["https://app.example.com"],
                    [
                        "https://second.net",
                        "https://third.org",
                        "https://fourth.io",
                        "https://fifth.dev",
                    ],
                    Algorithms)
                .Id);

    /// <summary>
    /// AUTH-FACT-012 AC2: a sixth label exceeds what a browser reads and stops the
    /// deployment.
    /// </summary>
    [Fact]
    public void AUTH_FACT_012_AC2_MoreThanFiveDistinctLabelsIsRefused() =>
        Assert.Equal(
            ErrorCodes.StartupLabelLimit,
            Refused(() => RelyingParty.Of(
                "example.com",
                ["https://app.example.com"],
                [
                    "https://second.net",
                    "https://third.org",
                    "https://fourth.io",
                    "https://fifth.dev",
                    "https://sixth.app",
                ],
                Algorithms)));

    /// <summary>
    /// AUTH-FACT-012 AC2: subdomains of one domain carry one label between them, so a
    /// deployment spread across them is not refused for its own subdomains.
    /// </summary>
    [Fact]
    public void AUTH_FACT_012_AC2_SubdomainsOfOneDomainCountOnce() =>
        Assert.Equal(
            "example.com",
            RelyingParty.Of(
                    "example.com",
                    ["https://app.example.com"],
                    [
                        "https://one.second.net",
                        "https://two.second.net",
                        "https://three.second.net",
                        "https://four.second.net",
                        "https://five.second.net",
                    ],
                    Algorithms)
                .Id);

    /// <summary>
    /// AUTH-FACT-010: the four keys the deployment names are where the relying party
    /// comes from.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_010_TheConfigurationIsWhereItComesFromAsync()
    {
        ConfigurationInMemory configuration = new();

        configuration.Set(Settings.WebAuthnRelyingPartyId, string.Empty);
        configuration.Set(
            Settings.WebAuthnOrigins,
            (IReadOnlyList<string>)["https://app.example.com", "https://id.example.com"]);
        configuration.Set(
            Settings.WebAuthnRelatedOrigins,
            (IReadOnlyList<string>)["https://example.net"]);

        RelyingParty party = await RelyingParty.ForAsync(
            configuration,
            TestContext.Current.CancellationToken);

        Assert.Equal("example.com", party.Id);
        Assert.Equal(["https://example.net"], party.RelatedOrigins);
        Assert.Equal([-8, -7, -257], party.Algorithms);
    }

    private static RelyingParty Settled(string identifier, IReadOnlyList<string> origins) =>
        RelyingParty.Of(identifier, origins, [], Algorithms);

    private static ErrorCode? Refusal(string identifier, IReadOnlyList<string> origins) =>
        Refused(() => Settled(identifier, origins));

    private static ErrorCode? Refused(System.Func<RelyingParty> settling) =>
        Assert.Throws<StartupException>(() => settling()).Failure?.Code;
}
