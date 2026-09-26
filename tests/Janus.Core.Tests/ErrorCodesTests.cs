using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The catalogue of error codes: its documentation, its stability and its rows in
/// chapter 10 (CONV-NAME-003, LIB-API-003, REF-001, BFF-ERR-001).
/// </summary>
[Trait("kind", "contract")]
public sealed class ErrorCodesTests
{
    // A code made from what the parse is given, which is a literal wherever the
    // library makes one.
    private static readonly Regex Parsing = new(
        @"ErrorCode\.Parse\(([^)]*)\)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly string[] Catalogue =
    [
        "api.request.malformed",
        "auth.breakglass.consumed",
        "auth.breakglass.invalid",
        "auth.challenge.required",
        "auth.code.expired",
        "auth.code.invalid",
        "auth.code.replayed",
        "auth.credential.labelinvalid",
        "auth.credential.lastsecondfactor",
        "auth.credential.notfound",
        "auth.credential.notupgradable",
        "auth.credential.suspended",
        "auth.device.verificationrequired",
        "auth.enrolment.tokeninvalid",
        "auth.factor.notpermitted",
        "auth.factor.rejected",
        "auth.factor.required",
        "auth.lossreport.notpermitted",
        "auth.lossreport.pending",
        "auth.oidc.nonconformant",
        "auth.password.blocklisted",
        "auth.password.toolong",
        "auth.password.tooshort",
        "auth.policy.graceexpired",
        "auth.recovery.channelnotonaccount",
        "auth.recovery.reasonrequired",
        "auth.recovery.selfapproval",
        "auth.recovery.tokenexpired",
        "auth.recovery.tokeninvalid",
        "auth.restriction.exceeded",
        "auth.restriction.reasonrequired",
        "auth.screening.unavailable",
        "auth.session.csrfinvalid",
        "auth.session.expired",
        "auth.stepup.required",
        "auth.stepup.unavailable",
        "auth.throttled",
        "auth.webauthn.algorithmnotallowed",
        "auth.webauthn.countermismatch",
        "auth.webauthn.rpidchanged",
        "auth.webauthn.userverificationrequired",
        "authz.denied",
        "authz.derivation.sourcesmissing",
        "authz.grant.duplicate",
        "authz.grant.expired",
        "authz.grant.notfound",
        "authz.grant.reasonrequired",
        "authz.group.cycle",
        "authz.group.inuse",
        "authz.policy.unregistered",
        "authz.resource.notfound",
        "authz.restricted",
        "authz.role.inuse",
        "authz.truthtable.disagreement",
        "config.change.stepuprequired",
        "config.key.protected",
        "config.policy.belowsystem",
        "config.value.aboveceiling",
        "config.value.belowfloor",
        "config.value.lastdestination",
        "config.value.notallowed",
        "identity.account.adminsuspended",
        "identity.affirmation.required",
        "identity.change.pending",
        "identity.change.windowelapsed",
        "identity.deletion.windowelapsed",
        "identity.domain.unverified",
        "identity.identifier.domainnotallowed",
        "identity.identifier.invalid",
        "identity.identifier.lastofkind",
        "identity.identifier.locked",
        "identity.identifier.maximum",
        "identity.identifier.mixedscript",
        "identity.identifier.primary",
        "identity.invitation.expired",
        "identity.invitation.identifiermismatch",
        "identity.invitation.notfound",
        "identity.link.lastcredential",
        "identity.membership.limitreached",
        "identity.organization.protected",
        "identity.photo.invalid",
        "identity.photo.notenabled",
        "identity.photo.toolarge",
        "identity.preference.administratoronly",
        "identity.preference.toolarge",
        "identity.preference.undeclared",
        "identity.preference.wrongtype",
        "identity.profile.invalid",
        "identity.profile.notaccepted",
        "identity.profile.underage",
        "identity.reactivation.tokeninvalid",
        "identity.registration.incomplete",
        "identity.registration.signedin",
        "identity.takedown.active",
        "identity.takedown.notfound",
        "identity.takedown.windowelapsed",
        "identity.username.coolingoff",
        "identity.username.invalid",
        "identity.username.reserved",
        "identity.username.taken",
        "integration.callback.rejected",
        "integration.endpoint.insecure",
        "integration.sms.balancefloor",
        "model.containment.cycle",
        "model.derivation.undeclaredreference",
        "model.derivation.unindexed",
        "model.purpose.missingassessment",
        "model.role.undeclaredpermission",
        "model.startup.declarationmissing",
        "model.startup.governinglanguage",
        "model.startup.kekunavailable",
        "model.startup.labellimit",
        "model.startup.preferencedeclaration",
        "model.startup.redirectclient",
        "model.startup.rpid",
        "model.startup.schemamismatch",
        "model.startup.subscribername",
        "model.type.noorganizationpath",
        "model.type.undeclaredreference",
        "privacy.consent.required",
        "privacy.consent.superseded",
        "privacy.consent.writtenrequired",
        "privacy.document.notfound",
        "privacy.erasure.notfailed",
        "privacy.erasure.notfound",
        "privacy.notice.governingtextmissing",
        "privacy.notice.unpublished",
        "privacy.purpose.noconsent",
        "privacy.purpose.notobjectable",
        "privacy.request.decided",
        "privacy.request.duplicate",
        "privacy.request.notfound",
        "privacy.request.receivedfuture",
        "system.fault",
    ];

    /// <summary>
    /// CONV-NAME-003 AC1: every code is documented with its meaning and what the
    /// caller does about it.
    /// </summary>
    [Fact]
    public void CONV_NAME_003_AC1_EveryCodeCarriesMeaningAndRemediation() =>
        AssertEveryCodeIsDocumented();

    /// <summary>
    /// LIB-API-003 AC2: the same documentation obligation seen from the boundary, where
    /// the host renders a sentence from the code and nothing else.
    /// </summary>
    [Fact]
    public void LIB_API_003_AC2_EveryCodeCarriesMeaningAndRemediation() =>
        AssertEveryCodeIsDocumented();

    /// <summary>
    /// CONV-NAME-003 AC2: a code that changes fails this test, so the change is made
    /// deliberately and carries its version bump.
    /// </summary>
    [Fact]
    public void CONV_NAME_003_AC2_ChangingACodeFailsTheContractTest()
    {
        var declared = Codes().Values.Order(StringComparer.Ordinal).ToList();

        Assert.Equal(Catalogue, declared);
    }

    /// <summary>
    /// REF-001 AC1: a code the catalogue holds and chapter 10 does not fails here,
    /// unless the ledger owes chapter 10 its row. A row the ledger owes that names no
    /// code of the catalogue fails as well, so what is owed cannot outlive the code it
    /// is owed for.
    /// </summary>
    [Fact]
    public void REF_001_AC1_EveryCodeInTheSourceIsARowOfTheReference()
    {
        string[] declared = [.. Codes().Values];

        Assert.Empty(Undocumented(declared));
        Assert.Empty(ReferenceRows.OwedCodes.Except(declared, StringComparer.Ordinal));
    }

    /// <summary>
    /// BFF-ERR-001 AC3: every code the boundary can answer with is a row of chapter 10
    /// or one the ledger owes it. A code is made only through
    /// <see cref="ErrorCode.Parse"/>, since no other constructor is reachable, so the
    /// codes a response can carry are the literals the library's source parses; no
    /// source parses a code it computed.
    /// </summary>
    [Fact]
    public void BFF_ERR_001_AC3_EveryCodeTheBoundaryCanAnswerIsInTheReference()
    {
        Assert.DoesNotContain(
            typeof(ErrorCode).GetConstructors(),
            constructor => constructor.GetParameters().Length > 0);

        string[] arguments =
        [
            .. Directory
                .EnumerateFiles(Path.Combine(Repository.Root, "src"), "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .SelectMany(file => Parsing.Matches(File.ReadAllText(file)))
                .Select(parsed => parsed.Groups[1].Value.Trim()),
        ];

        Assert.NotEmpty(arguments);
        Assert.All(arguments, argument => Assert.Matches("^\"[^\"]+\"$", argument));
        Assert.Empty(Undocumented([.. arguments.Select(argument => argument.Trim('"'))]));
    }

    private static void AssertEveryCodeIsDocumented()
    {
        var documentation = XDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Janus.Core.xml")));

        foreach (string name in Codes().Keys)
        {
            XElement? summary = documentation
                .Descendants("member")
                .FirstOrDefault(member =>
                    string.Equals(member.Attribute("name")?.Value, "P:Janus.Core.ErrorCodes." + name, StringComparison.Ordinal))
                ?.Element("summary");

            Assert.NotNull(summary);
            Assert.True(
                summary.Value.Count(character => character == '.') >= 2,
                name + " states a meaning but no remediation.");
        }
    }

    // The codes of those given that chapter 10 holds no live row for and the ledger
    // does not owe it.
    private static string[] Undocumented(IReadOnlyList<string> codes) =>
        [.. codes.Except(ReferenceRows.ChapterCodes.Concat(ReferenceRows.OwedCodes), StringComparer.Ordinal)];

    private static Dictionary<string, string> Codes() =>
        typeof(ErrorCodes)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .ToDictionary(
                property => property.Name,
                property => property.GetValue(null)!.ToString()!,
                StringComparer.Ordinal);
}
