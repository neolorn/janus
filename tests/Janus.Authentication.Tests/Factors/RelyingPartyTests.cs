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
    /// AUTH-FACT-010 AC1: with no identifier set, origins that share no label leave no
    /// identifier to derive, which is refused with the same code and never a fault.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_AC1_OriginsThatShareNoDomainAreRefused() =>
        Assert.Equal(
            ErrorCodes.StartupRelyingPartyId,
            Refusal(string.Empty, ["https://app.example.com", "https://id.example.org"]));

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
    /// AUTH-FACT-010: each configured origin is held in its serialization, the form a
    /// browser writes into a ceremony's client data: its scheme, its host in the ASCII
    /// form the conversion gives, and its port only where it is not the scheme's
    /// default.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_AConfiguredOriginIsHeldInItsSerialization() =>
        Assert.Equal(
            [
                "https://app.xn--bcher-kva.de",
                "https://id.xn--bcher-kva.de",
                "https://id.xn--bcher-kva.de:8443",
                "https://xn--bcher-kva.de",
            ],
            Settled(
                "xn--bcher-kva.de",
                [
                    "https://app.bücher.de",
                    "HTTPS://ID.Bücher.de:443",
                    "https://id.xn--bcher-kva.de:8443",
                    "https://BÜCHER.de/",
                ]).Origins);

    /// <summary>
    /// AUTH-FACT-010: a related origin is held in its serialization as a configured one
    /// is.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_ARelatedOriginIsHeldInItsSerialization() =>
        Assert.Equal(
            ["https://xn--bcher-kva.de", "https://example.net:8443"],
            RelyingParty.Of(
                    "example.com",
                    ["https://app.example.com"],
                    ["https://Bücher.de:443", "https://EXAMPLE.net:8443/"],
                    Algorithms)
                .RelatedOrigins);

    /// <summary>
    /// AUTH-FACT-012 AC1: the well-known document lists each configured related origin
    /// in its serialization, the form a browser's URL parser gives an entry before it
    /// compares it with the caller's origin.
    /// </summary>
    [Fact]
    public void AUTH_FACT_012_AC1_TheDocumentListsEachRelatedOriginInItsSerialization() =>
        Assert.Equal(
            """{"origins":["https://xn--bcher-kva.de","https://example.org:8443"]}""",
            RelyingParty.Of(
                    "example.com",
                    ["https://app.example.com"],
                    ["https://bücher.de", "https://example.org:8443"],
                    Algorithms)
                .Allowlist());

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
    /// AUTH-FACT-012 AC2: <c>shop.com</c> and <c>shop.co.uk</c> carry one label between
    /// them, the name before each public suffix, so five labels stand with both.
    /// </summary>
    [Fact]
    public void AUTH_FACT_012_AC2_ShopComAndShopCoUkCountAsOneLabel() =>
        Assert.Equal(
            "shop.com",
            RelyingParty.Of(
                    "shop.com",
                    ["https://www.shop.com"],
                    [
                        "https://shop.co.uk",
                        "https://second.net",
                        "https://third.org",
                        "https://fourth.io",
                    ],
                    Algorithms)
                .Id);

    /// <summary>
    /// AUTH-FACT-012 AC2: <c>a.co.uk</c> and <c>b.co.uk</c> carry two labels, so beside
    /// four others they exceed what a browser reads.
    /// </summary>
    [Fact]
    public void AUTH_FACT_012_AC2_ACoUkAndBCoUkCountAsTwoLabels() =>
        Assert.Equal(
            ErrorCodes.StartupLabelLimit,
            Refused(() => RelyingParty.Of(
                "example.com",
                ["https://app.example.com"],
                [
                    "https://a.co.uk",
                    "https://b.co.uk",
                    "https://third.org",
                    "https://fourth.io",
                    "https://fifth.dev",
                ],
                Algorithms)));

    /// <summary>
    /// AUTH-FACT-012 AC2: the list's private section applies as its ICANN section does,
    /// so two names under one hosting suffix carry two labels.
    /// </summary>
    [Fact]
    public void AUTH_FACT_012_AC2_ThePrivateSectionCountsAsTheIcannSectionDoes() =>
        Assert.Equal(
            ErrorCodes.StartupLabelLimit,
            Refused(() => RelyingParty.Of(
                "example.com",
                ["https://app.example.com"],
                [
                    "https://first.github.io",
                    "https://second.github.io",
                    "https://third.org",
                    "https://fourth.io",
                    "https://fifth.dev",
                ],
                Algorithms)));

    /// <summary>
    /// AUTH-FACT-010 AC1: an identifier that is a public suffix of more than one label,
    /// under which anyone may register, is no registrable suffix and is refused.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_AC1_AnIdentifierThatIsAPublicSuffixIsRefused()
    {
        Assert.Equal(ErrorCodes.StartupRelyingPartyId, Refusal("co.uk", ["https://shop.co.uk"]));
        Assert.Equal(ErrorCodes.StartupRelyingPartyId, Refusal("github.io", ["https://someone.github.io"]));
    }

    /// <summary>
    /// AUTH-FACT-010 AC2: the common parent derived is a registrable domain, and origins
    /// that share only a public suffix share nothing a passkey could be bound to.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_AC2_TheDerivedParentIsARegistrableDomain()
    {
        Assert.Equal(
            "shop.co.uk",
            Settled(string.Empty, ["https://app.shop.co.uk", "https://id.shop.co.uk"]).Id);
        Assert.Equal(
            ErrorCodes.StartupRelyingPartyId,
            Refusal(string.Empty, ["https://a.co.uk", "https://b.co.uk"]));
    }

    /// <summary>
    /// AUTH-FACT-010: the four keys the deployment names are where the relying party
    /// comes from.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_FACT_010_TheConfigurationIsWhereItComesFromAsync()
    {
        ConfigurationInMemory configuration = new();

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

    /// <summary>
    /// AUTH-FACT-010: the origins a ceremony may run from are read at startup, and an
    /// entry that is not an absolute origin stops the deployment rather than being
    /// carried as something a ceremony could later be matched against.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_AnEntryThatIsNotAnAbsoluteOriginFails() =>
        Assert.Equal(
            ErrorCodes.StartupRelyingPartyId,
            Refusal("example.com", ["https://app.example.com", "/signin/callback"]));

    /// <summary>
    /// AUTH-FACT-010 AC1: an origin whose host the library's conversion gives no ASCII
    /// form has no registrable domain for an identifier to sit over, and an identifier
    /// with none sits over nothing; each is refused with the same code and never a fault.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_AC1_ANameWithNoAsciiFormIsRefused()
    {
        Assert.Equal(
            ErrorCodes.StartupRelyingPartyId,
            Refusal("example.com", ["https://app.example.com", "https://my_shop.example.com"]));
        Assert.Equal(
            ErrorCodes.StartupRelyingPartyId,
            Refusal("ab--c.com", ["https://app.ab--c.com"]));
        Assert.Equal(
            ErrorCodes.StartupRelyingPartyId,
            Refused(() => RelyingParty.Of(
                "example.com",
                ["https://app.example.com"],
                ["https://my_shop.example.net"],
                Algorithms)));
    }

    /// <summary>
    /// AUTH-FACT-010 AC5: an identifier written in ASCII form sits over an origin whose
    /// host is written in Unicode, and settles as it would with both in ASCII form.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_AC5_AnAsciiIdentifierSitsOverAnOriginWrittenInUnicode() =>
        Assert.Equal(
            "xn--bcher-kva.de",
            Settled("xn--bcher-kva.de", ["https://app.bücher.de", "https://BÜCHER.de"]).Id);

    /// <summary>
    /// AUTH-FACT-010 AC5: an identifier written in Unicode sits over an origin whose
    /// host is written in ASCII form, and settles as it would with both in ASCII form.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_AC5_AUnicodeIdentifierSitsOverAnOriginWrittenInAscii() =>
        Assert.Equal(
            "xn--bcher-kva.de",
            Settled("Bücher.de", ["https://app.xn--bcher-kva.de", "https://id.bücher.de"]).Id);

    /// <summary>
    /// AUTH-FACT-010 AC5: with no identifier set, origins written in the two forms share
    /// the parent they would share in ASCII form, and that form is what is derived.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_AC5_AnUnsetIdentifierDerivesInAsciiForm()
    {
        Assert.Equal(
            "xn--bcher-kva.de",
            Settled(string.Empty, ["https://app.bücher.de", "https://id.xn--bcher-kva.de"]).Id);
        Assert.Equal(
            "xn--bcher-kva.de",
            Settled(string.Empty, ["https://app.bücher.de", "https://id.bücher.de"]).Id);
    }

    /// <summary>
    /// AUTH-FACT-010 AC5: the form is no way round the comparison, so an identifier in
    /// either form over an origin of another domain is refused as in ASCII form.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_AC5_AnIdentifierOverNoOriginIsRefusedInEitherForm()
    {
        Assert.Equal(
            ErrorCodes.StartupRelyingPartyId,
            Refusal("bücher.de", ["https://app.xn--bcher-kva.com"]));
        Assert.Equal(
            ErrorCodes.StartupRelyingPartyId,
            Refusal("xn--bcher-kva.de", ["https://app.bucher.de"]));
    }

    /// <summary>
    /// AUTH-FACT-012 AC2: the labels counted are compared in their ASCII form, so one
    /// name written in Unicode and in its ASCII form counts once.
    /// </summary>
    [Fact]
    public void AUTH_FACT_012_AC2_ALabelWrittenInTwoFormsCountsOnce()
    {
        var party = RelyingParty.Of(
            "example.com",
            ["https://app.example.com"],
            [
                "https://bücher.de",
                "https://xn--bcher-kva.com",
                "https://one.net",
                "https://two.net",
                "https://three.net",
            ],
            Algorithms);

        Assert.Equal(5, party.RelatedOrigins.Count);
    }

    private static RelyingParty Settled(string identifier, IReadOnlyList<string> origins) =>
        RelyingParty.Of(identifier, origins, [], Algorithms);

    private static ErrorCode? Refusal(string identifier, IReadOnlyList<string> origins) =>
        Refused(() => Settled(identifier, origins));

    private static ErrorCode? Refused(System.Func<RelyingParty> settling) =>
        Assert.Throws<StartupException>(() => settling()).Failure?.Code;
}
