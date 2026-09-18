using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The catalogue as a contract: which keys exist, which the deployment has to name,
/// which the application cannot change, and that every other key resolves with a
/// default (LIB-API-001, LIB-HOST-001, OPS-CFG-001, OPS-CFG-004).
/// </summary>
[Trait("kind", "contract")]
public sealed class SettingsCatalogueTests
{
    // Every key chapter 10 section 4 marks P. Section 4.8 holds the ones OPS-CFG-004
    // protects because turning them off would hide the person turning them off; the
    // rest are marked P where they are declared, because they are facts about the
    // deployment rather than runtime controls.
    private static readonly string[] Protected =
    [
        "abuse.throttle.enabled",
        "alerting.destinationchange.notify",
        "audit.enabled",
        "exfiltration.export.auditing",
        "hosting.crossborderbasis",
        "hosting.location",
        "legal.governinglanguage",
        "stepup.enforcement",
        "token.signature.verification",
        "token.signing.algorithm",
        "webauthn.algorithms",
        "webauthn.origins",
        "webauthn.rpid",
    ];

    // The eight keys of LIB-HOST-001 that name the deployment, and the one that is
    // required only when hosting is outside Egypt.
    private static readonly string[] NamedByTheDeployment =
    [
        "abuse.sms.balancefloor",
        "alerting.email.destinations",
        "alerting.owner.email",
        "alerting.owner.sms",
        "alerting.sms.destinations",
        "hosting.crossborderbasis",
        "hosting.location",
        "legal.governinglanguage",
        "webauthn.origins",
    ];

    /// <summary>
    /// LIB-HOST-001 AC1: a minimal working configuration names only the declarations
    /// the item lists, so every other key hands back a default rather than throwing.
    /// </summary>
    [Fact]
    public void LIB_HOST_001_AC1_EveryOtherKeyHasADefault()
    {
        foreach (Setting setting in Settings.All.Where(setting => !setting.IsRequired))
        {
            Assert.NotNull(Resolve(setting));
        }
    }

    /// <summary>
    /// LIB-HOST-001 AC3: no key outside the declarations is one the deployment has to
    /// name.
    /// </summary>
    [Fact]
    public void LIB_HOST_001_AC3_NoKeyOutsideTheDeclarationsIsRequired()
    {
        string[] required = [.. Settings.Required.Select(setting => setting.Key.ToString()).Order(StringComparer.Ordinal)];

        Assert.Equal(NamedByTheDeployment, required);
    }

    /// <summary>
    /// Chapter 10 section 4 marks each key R or P, and the catalogue carries the mark
    /// the chapter gives it.
    /// </summary>
    [Fact]
    public void Scope_TheCatalogue_ProtectsTheKeysSectionFourMarks()
    {
        IEnumerable<string> protectedKeys = Settings.All
            .Where(setting => setting.Scope == SettingScope.Protected)
            .Select(setting => setting.Key.ToString())
            .Concat(Settings.Families
                .Where(family => family.Scope == SettingScope.Protected)
                .Select(family => family.Prefix));

        Assert.Equal(Protected.Order(StringComparer.Ordinal), protectedKeys.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// LIB-API-001: the key names are the contract, so a key that is renamed, added or
    /// dropped fails here and carries its version bump.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_TheKeyNamesAreTheContract()
    {
        string[] declared = [.. Settings.All.Select(setting => setting.Key.ToString()).Order(StringComparer.Ordinal)];

        Assert.Equal(
            Repository.ReadText("tests/Janus.Core.Tests/configuration-keys.txt")
                .ReplaceLineEndings("\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries),
            declared);
    }

    /// <summary>
    /// LIB-API-001: the families are part of the same contract, and each states
    /// whether the library has a value for a member the deployment never wrote.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_TheFamiliesAreTheContract()
    {
        string[] declared = [.. Settings.Families.Select(family => family.Prefix).Order(StringComparer.Ordinal)];

        Assert.Equal(["policy", "retention", "stepup.enforcement"], declared);
    }

    /// <summary>
    /// Every key is named once. A duplicate would make one of the two unreachable
    /// through the store.
    /// </summary>
    [Fact]
    public void All_TheCatalogue_NamesEveryKeyOnce() =>
        Assert.Equal(
            Settings.All.Count,
            Settings.All.Select(setting => setting.Key.ToString()).Distinct(StringComparer.Ordinal).Count());

    // Reading the default of a setting whose type is only known at runtime. A setting
    // that resolves hands back a value; one that does not throws, which is the failure
    // LIB-HOST-001 AC1 is about.
    private static object Resolve(Setting setting)
    {
        Type typed = setting.GetType();

        while (!typed.IsGenericType || typed.GetGenericTypeDefinition() != typeof(Setting<>))
        {
            typed = typed.BaseType!;
        }

        return typed.GetProperty(nameof(Setting<object>.Default))!.GetValue(setting)!;
    }
}
