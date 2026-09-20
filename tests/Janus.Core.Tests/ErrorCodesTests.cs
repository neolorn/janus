using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The catalogue of error codes: its documentation and its stability
/// (CONV-NAME-003, LIB-API-003).
/// </summary>
[Trait("kind", "contract")]
public sealed class ErrorCodesTests
{
    private static readonly string[] Catalogue =
    [
        "auth.challenge.required",
        "auth.code.expired",
        "auth.code.invalid",
        "auth.code.replayed",
        "auth.device.verificationrequired",
        "auth.factor.notpermitted",
        "auth.factor.rejected",
        "auth.factor.required",
        "auth.password.blocklisted",
        "auth.password.tooshort",
        "auth.policy.graceexpired",
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
        "authz.policy.unregistered",
        "authz.restricted",
        "config.change.stepuprequired",
        "config.key.protected",
        "config.policy.belowsystem",
        "config.value.aboveceiling",
        "config.value.belowfloor",
        "config.value.lastdestination",
        "config.value.notallowed",
        "identity.affirmation.required",
        "identity.change.pending",
        "identity.change.windowelapsed",
        "identity.identifier.lastofkind",
        "identity.identifier.mixedscript",
        "identity.identifier.primary",
        "identity.organization.protected",
        "identity.preference.administratoronly",
        "identity.preference.toolarge",
        "identity.preference.undeclared",
        "identity.preference.wrongtype",
        "identity.profile.underage",
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
        "model.startup.labellimit",
        "model.startup.preferencedeclaration",
        "model.startup.rpid",
        "model.type.noorganizationpath",
        "model.type.undeclaredreference",
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

    private static Dictionary<string, string> Codes() =>
        typeof(ErrorCodes)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .ToDictionary(
                property => property.Name,
                property => property.GetValue(null)!.ToString()!,
                StringComparer.Ordinal);
}
