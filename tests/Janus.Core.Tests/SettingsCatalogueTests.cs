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
    // Every key chapter 10 section 4 marks P: the nine of section 4.8 and the four
    // more that are marked where they are declared, because they are facts about the
    // deployment rather than runtime controls.
    private static readonly string[] Protected =
    [
        .. ProtectedBySectionFourEight,
        "hosting.crossborderbasis",
        "hosting.location",
        "integration.mail.endpoint",
        "integration.sms.endpoint",
        "webauthn.algorithms",
        "webauthn.origins",
    ];

    // The eleven keys of LIB-HOST-001 that name the deployment, and the three that
    // are named only where their condition holds.
    private static readonly string[] NamedByTheDeployment =
    [
        "abuse.sms.balancefloor",
        "alerting.email.destinations",
        "alerting.owner.email",
        "alerting.owner.sms",
        "alerting.sms.destinations",
        "hosting.crossborderbasis",
        "hosting.environment",
        "hosting.location",
        "legal.governinglanguage",
        "notification.email.sendingdomain",
        "notification.languages",
        "password.blocklist.selfhosted.address",
        "privacy.calendar.timezone",
        "service.name",
        "webauthn.origins",
    ];

    // Chapter 10 section 4.8, which OPS-CFG-004 states is the same list as its own:
    // the settings whose change would hide the person changing them, and the
    // governing language, which is protected because the text a document binds in is
    // not a runtime toggle.
    private static string[] ProtectedBySectionFourEight =>
    [
        "abuse.throttle.enabled",
        "audit.enabled",
        "exfiltration.export.auditing",
        "legal.governinglanguage",
        "privacy.calendar.timezone",
        "stepup.enforcement",
        "token.signature.verification",
        "token.signing.algorithm",
        "webauthn.rpid",
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
    /// OPS-CFG-004 and chapter 10 section 4.8 are one list, so every key section 4.8
    /// holds is one the application cannot change.
    /// </summary>
    [Fact]
    public void Scope_TheOpsCfg004List_IsProtectedInTheCatalogue()
    {
        foreach (string key in ProtectedBySectionFourEight)
        {
            Assert.Equal(SettingScope.Protected, ScopeOf(key));
        }
    }

    /// <summary>
    /// The chapter 10 section 4 direction rule: a key bounded only above loosens
    /// upward, a key bounded only below loosens downward, and a key bounded at
    /// neither end loosens on any change.
    /// </summary>
    [Fact]
    public void Loosening_AKeyWhoseRowNamesNoDirection_IsReadFromItsBounds()
    {
        Assert.Equal(SettingDirection.Increase, Settings.SessionAal2Inactivity.Loosening);
        Assert.Equal(SettingDirection.Decrease, Settings.PasswordMaximum.Loosening);
        Assert.Equal(SettingDirection.AnyChange, Settings.SessionStepUpRecency.Loosening);
    }

    /// <summary>
    /// The same rule for a boolean, which loosens away from its default whichever way
    /// that is, and for the one family of booleans.
    /// </summary>
    [Fact]
    public void Loosening_ABoolean_LoosensAwayFromItsDefault()
    {
        Assert.Equal(SettingDirection.Decrease, Settings.AuditEnabled.Loosening);
        Assert.Equal(SettingDirection.Increase, Settings.AlertingOwnerEnabled.Loosening);
        Assert.Equal(SettingDirection.Decrease, Settings.OrganizationStepUpEnforcement.Loosening);
    }

    /// <summary>
    /// Where a row names a direction, that direction governs rather than the one the
    /// bounds would give: the identifier maximum is bounded only below and yet
    /// loosens upward, because each verified address is a send destination.
    /// </summary>
    [Fact]
    public void Loosening_AKeyWhoseRowNamesADirection_CarriesTheNamedOne()
    {
        Assert.Equal(SettingDirection.Increase, Settings.IdentifiersEmailMax.Loosening);
        Assert.Equal(SettingDirection.Increase, Settings.IdentifiersPhoneMax.Loosening);
        Assert.Equal(SettingDirection.Decrease, Settings.PasswordBlocklistSources.Loosening);
    }

    /// <summary>
    /// The chapter 10 section 4 holding rule: a duration written in years is held at
    /// 366 days a year and one written in months at 31 days a month, so a floor
    /// stated in either is never shorter than the calendar span it names.
    /// </summary>
    [Fact]
    public void Default_ADurationInYearsOrMonths_IsHeldLong()
    {
        Assert.Equal(TimeSpan.FromDays(7 * 366), Settings.RetentionAuditSecurity.Default);
        Assert.Equal(TimeSpan.FromDays(5 * 366), Settings.RetentionAuditSecurity.Floor);
        Assert.Equal(TimeSpan.FromDays(3 * 366), Settings.RetentionConsent.Default);
        Assert.Equal(TimeSpan.FromDays(366), Settings.RetentionConsent.Floor);
        Assert.Equal(TimeSpan.FromDays(3 * 31), Settings.BackupRestoreTestInterval.Default);
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

    // The scope the catalogue carries for a key or a family, by its written name.
    private static SettingScope ScopeOf(string key) =>
        Settings.All.SingleOrDefault(setting => setting.Key.ToString() == key)?.Scope
            ?? Settings.Families.Single(family => family.Prefix == key).Scope;

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
