using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The catalogue as a contract: which keys exist, which the deployment has to name,
/// which the application cannot change, and that every other key resolves with a
/// default, and that each is a row of chapter 10 (LIB-API-001, LIB-HOST-001,
/// OPS-CFG-001, OPS-CFG-004, REF-001).
/// </summary>
[Trait("kind", "contract")]
public sealed class SettingsCatalogueTests
{
    // The constraints a setting can hold on its value, by the name the catalogue gives
    // each.
    private static readonly string[] Constraints = ["Floor", "Ceiling", "Allowed", "Minimum", "Unremovable"];

    // The capital that begins each word of a name after the first.
    private static readonly Regex Words = new("(?<=.)([A-Z])", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

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
    /// OPS-CFG-001 AC1: a key the application cannot change is on the OPS-CFG-004 list,
    /// which is chapter 10 section 4.8: the protected keys and families of the
    /// catalogue are exactly the rows of that section. Every other key is
    /// runtime-changeable.
    /// </summary>
    [Fact]
    public void OPS_CFG_001_AC1_ARedeployScopedKeyIsOnTheOpsCfg004List()
    {
        string[] redeployScoped =
        [
            .. Settings.All
                .Where(setting => setting.Scope == SettingScope.Protected)
                .Select(setting => setting.Key.ToString())
                .Concat(Settings.Families
                    .Where(family => family.Scope == SettingScope.Protected)
                    .Select(family => ReferenceRows.Family(family.Prefix)))
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(ReferenceRows.ProtectedList.Order(StringComparer.Ordinal), redeployScoped);
    }

    /// <summary>
    /// OPS-CFG-004: the list and chapter 10 section 4.8 are one list, so a key whose row
    /// in chapter 10 section 4 marks it P is a row of section 4.8, and no key is marked
    /// P outside it (D-152).
    /// </summary>
    [Fact]
    public void OPS_CFG_004_AKeyMarkedProtectedIsOnTheOneList() =>
        Assert.Empty(ReferenceRows.MarkedProtected.Except(ReferenceRows.ProtectedList, StringComparer.Ordinal));

    /// <summary>
    /// LIB-API-001 AC2: the keys are the contract, each with its type, its scope and
    /// every constraint the catalogue holds on its value, and the families with their
    /// scopes, so a key renamed, added, dropped, retyped, rescoped or bounded otherwise,
    /// and a family added or dropped, fails here and carries its version bump.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_TheKeysAreTheContract()
    {
        string[] declared =
        [
            .. Settings.All.Select(Contracted),
            .. Settings.Families.Select(family => family.Prefix + " family " + Scope(family.Scope)),
        ];

        Assert.Equal(
            Repository.ReadText("tests/Janus.Core.Tests/configuration-keys.txt")
                .ReplaceLineEndings("\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries),
            declared.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// REF-001 AC1: a key or a family the catalogue holds that is no live row of
    /// chapter 10 fails here. The test reads chapter 10 alone (D-183).
    /// </summary>
    [Fact]
    public void REF_001_AC1_EveryKeyInTheSourceIsARowOfTheReference()
    {
        string[] declared =
        [
            .. Settings.All.Select(setting => setting.Key.ToString()),
            .. Settings.Families.Select(family => ReferenceRows.Family(family.Prefix)),
        ];

        Assert.Empty(declared.Except(ReferenceRows.ChapterKeys, StringComparer.Ordinal));
    }

    /// <summary>
    /// REF-001 AC1: a live row of a key table of chapter 10 section 4 naming a key the
    /// catalogue does not hold fails here. The fields of section 4.1a are no keys, and
    /// their table is no key table; a struck row, and one whose cell after the key opens
    /// "Retired" or "Withdrawn", is not live; a key written with a placeholder names a
    /// family, which the catalogue holds where it holds the family or any key of it.
    /// </summary>
    [Fact]
    public void REF_001_AC1_EveryLiveRowOfTheReferenceIsAKeyInTheSource()
    {
        string placeholder = ReferenceRows.Family(string.Empty)[1..];
        HashSet<string> declared =
        [
            .. Settings.All.Select(setting => setting.Key.ToString()),
            .. Settings.Families.Select(family => ReferenceRows.Family(family.Prefix)),
        ];

        Assert.DoesNotContain(
            ReferenceRows.ChapterKeys,
            row => !declared.Contains(row)
                && !(row.Contains(placeholder, StringComparison.Ordinal)
                    && declared.Any(key => key.StartsWith(
                        row[..row.IndexOf(placeholder, StringComparison.Ordinal)],
                        StringComparison.Ordinal))));
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
    /// that is.
    /// </summary>
    [Fact]
    public void Loosening_ABoolean_LoosensAwayFromItsDefault()
    {
        Assert.Equal(SettingDirection.Decrease, Settings.AbuseThrottleEnabled.Loosening);
        Assert.Equal(SettingDirection.Increase, Settings.AlertingOwnerEnabled.Loosening);
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
        Assert.Equal(
            TimeSpan.FromDays(2 * 31),
            Settings.BackupRestoreTestInterval.Read("P2M").Match(value => value, _ => TimeSpan.Zero));
    }

    /// <summary>
    /// OPS-CFG-003 and chapter 10 section 4: the outbox interval paces the carrying of
    /// raised alerts, and its ceiling of a minute is what keeps one loosening from
    /// holding every alert back.
    /// </summary>
    [Fact]
    public void OPS_CFG_003_TheOutboxIntervalHasItsCeiling() =>
        Assert.Equal(TimeSpan.FromMinutes(1), Settings.OutboxPollInterval.Ceiling);

    /// <summary>
    /// Every key is named once. A duplicate would make one of the two unreachable
    /// through the store.
    /// </summary>
    [Fact]
    public void All_TheCatalogue_NamesEveryKeyOnce() =>
        Assert.Equal(
            Settings.All.Count,
            Settings.All.Select(setting => setting.Key.ToString()).Distinct(StringComparer.Ordinal).Count());

    // One key's line of the contract: its name, its type, its scope and each
    // constraint the catalogue holds on its value, by the name the catalogue gives it.
    private static string Contracted(Setting setting)
    {
        Type type = setting.GetType();
        StringBuilder line = new StringBuilder()
            .Append(setting.Key.ToString())
            .Append(' ')
            .Append(Kind(type))
            .Append(' ')
            .Append(Scope(setting.Scope));

        foreach (string constraint in Constraints)
        {
            if (type.GetProperty(constraint)?.GetValue(setting) is { } held && Written(constraint, held) is { Length: > 0 } text)
            {
                line.Append(' ')
                    .Append(CultureInfo.InvariantCulture.TextInfo.ToLower(constraint))
                    .Append('=')
                    .Append(text);
            }
        }

        return line.ToString();
    }

    // The type a key's value has, read from the kind of setting it is declared as.
    private static string Kind(Type type)
    {
        string name = type.Name.Split('`')[0];

        return CultureInfo.InvariantCulture.TextInfo.ToLower(
            Words.Replace(name[..^nameof(Setting).Length], "-$1"));
    }

    private static string Scope(SettingScope scope) => scope == SettingScope.Protected ? "P" : "R";

    // A bound as the reference writes it, a set as its members in order, and a set
    // or a minimum that holds nothing back as nothing.
    private static string Written(string constraint, object held) => held switch
    {
        TimeSpan span => XmlConvert.ToString(span),
        0 when constraint == "Minimum" => string.Empty,
        IEnumerable members => string.Join(
            ',',
            members.Cast<object>().Select(SettingText.Of).Order(StringComparer.Ordinal)),
        _ => SettingText.Of(held),
    };

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
