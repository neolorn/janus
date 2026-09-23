using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// What the settings table holds for a key, and what comes back from it
/// (OPS-CFG-008).
/// </summary>
/// <remarks>
/// Every key of chapter 10 section 4 is read through the configuration store, so
/// every value type has to survive the round trip through the one column the table
/// has. A value the key no longer admits comes back as a failure rather than as a
/// value or an exception.
/// </remarks>
public sealed class SettingWrittenFormTests
{
    /// <summary>
    /// Every key of the catalogue writes its default and reads the same value back.
    /// The keys whose default is no value at all, one derived at startup, two named
    /// only where the shipped transport is used, one the deployment names and one
    /// naming a registered client, are the only ones the round trip passes over.
    /// </summary>
    [Fact]
    public void Written_EveryDefaultOfTheCatalogue_ReadsBackAsItself()
    {
        List<string> passedOver = [];

        foreach (Setting setting in Settings.All.Where(setting => !setting.IsRequired))
        {
            if (!RoundTrips(setting))
            {
                passedOver.Add(setting.Key.ToString());
            }
        }

        Assert.Equal(
            [
                "webauthn.rpid",
                "integration.mail.endpoint",
                "integration.sms.endpoint",
                "backup.restoretest.canary",
                "redirect.defaultclient",
            ],
            passedOver);
    }

    /// <summary>
    /// A duration is written as chapter 10 section 4 writes it.
    /// </summary>
    [Fact]
    public void Written_ADuration_IsTheFormTheChapterWrites() =>
        Assert.Equal("PT30M", Settings.AbuseNonexistentWindow.Write(TimeSpan.FromMinutes(30)));

    /// <summary>
    /// A value of a stated set is written as the wire contract names it, not as the
    /// number behind it.
    /// </summary>
    [Fact]
    public void Written_AValueOfAStatedSet_IsTheNameTheContractGivesIt() =>
        Assert.Equal("normal", Settings.AlertingSmsSeverityThreshold.Write(AlertSeverity.Normal));

    /// <summary>
    /// A set of a stated vocabulary is written as the names, and read back as the
    /// members.
    /// </summary>
    [Fact]
    public void Written_ASetOfAVocabulary_ReadsBackAsItsMembers()
    {
        string written = Settings.AbuseBotDefenceSignals.Write(
            new HashSet<BotDefenceSignal> { BotDefenceSignal.DatacenterRange });

        Assert.Equal("[\"datacenterRange\"]", written);
        Assert.Equal(
            new HashSet<BotDefenceSignal> { BotDefenceSignal.DatacenterRange },
            Read(Settings.AbuseBotDefenceSignals, written));
    }

    /// <summary>
    /// The shipped restriction set survives the round trip with every key, purpose
    /// and bucket it was written with.
    /// </summary>
    [Fact]
    public void Written_TheRestrictionSet_ReadsBackWithEveryBucket()
    {
        IReadOnlyList<Restriction> read = Read(
            Settings.Restrictions,
            Settings.Restrictions.Write(Settings.Restrictions.Default))!;

        Assert.Equal(Settings.Restrictions.Default.Count, read.Count);
        Assert.All(
            read.Zip(Settings.Restrictions.Default),
            pair =>
            {
                Assert.Equal(pair.Second.Name, pair.First.Name);
                Assert.Equal(pair.Second.Key, pair.First.Key);
                Assert.Equal(pair.Second.Purpose, pair.First.Purpose);
                Assert.Equal(pair.Second.Buckets, pair.First.Buckets);
            });
    }

    /// <summary>
    /// A host-supplied key keeps the name after the colon through the round trip.
    /// </summary>
    [Fact]
    public void Written_AHostSuppliedKey_KeepsTheNameAfterTheColon()
    {
        IReadOnlyList<Restriction> set =
        [
            new("tenant.sends", RestrictionKeyKind.Host, "tenant", RestrictionPurpose.Any,
                [new Bucket(4, TimeSpan.FromHours(2), BucketWindow.Fixed)]),
        ];

        IReadOnlyList<Restriction> read = Read(Settings.Restrictions, Settings.Restrictions.Write(set))!;

        Assert.Equal("tenant", read[0].HostKeyName);
        Assert.Equal(RestrictionKeyKind.Host, read[0].Key);
    }

    /// <summary>
    /// The system policy survives the round trip with its factors and its gates.
    /// </summary>
    [Fact]
    public void Written_TheSystemPolicy_ReadsBackWithItsGates()
    {
        Policy read = Read(Settings.PolicyDefault, Settings.PolicyDefault.Write(Settings.PolicyDefault.Default))!;

        Assert.Equal(Settings.PolicyDefault.Default.LoginFactors, read.LoginFactors);
        Assert.Equal(Settings.PolicyDefault.Default.Gates.Count, read.Gates.Count);
        Assert.All(
            Settings.PolicyDefault.Default.Gates,
            bound => Assert.Equal(bound.Value, read.Gates[bound.Key]));
        Assert.Equal(Settings.PolicyDefault.Default.RequiredAssurance, read.RequiredAssurance);
    }

    /// <summary>
    /// Chapter 10 section 4.1a: a gate is written with its <c>level</c>,
    /// <c>phishingResistant</c> and <c>maxAge</c>, in the system policy and in an
    /// organization's override alike.
    /// </summary>
    [Fact]
    public void Written_AGate_NamesItsFieldsAsThePolicyObjectDoes()
    {
        PolicyOverride stated = PolicyOverride.None with
        {
            Gates = new Dictionary<StepUpAction, Gate>
            {
                [StepUpAction.FactorRemove] = new(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(5)),
            },
        };

        using var system = JsonDocument.Parse(Settings.PolicyDefault.Write(Settings.PolicyDefault.Default));
        using var organization = JsonDocument.Parse(Settings.OrganizationPolicy.Write(stated));

        JsonElement gate = organization.RootElement.GetProperty("gates").GetProperty("factor:remove");

        Assert.Equal(
            ["level", "phishingResistant", "maxAge"],
            gate.EnumerateObject().Select(field => field.Name));
        Assert.Equal("PT5M", gate.GetProperty("maxAge").GetString());
        Assert.All(
            system.RootElement.GetProperty("gates").EnumerateObject(),
            bound => Assert.True(bound.Value.TryGetProperty("maxAge", out _)));
    }

    /// <summary>
    /// An organization's override survives the round trip with the fields it states
    /// and the absence of the fields it does not.
    /// </summary>
    [Fact]
    public void Written_AnOrganizationOverride_KeepsWhatItStatesAndWhatItLeaves()
    {
        var stated = new PolicyOverride(
            AssuranceLevel.Aal2,
            null,
            new Dictionary<StepUpAction, Gate>
            {
                [StepUpAction.FactorRemove] = new(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(5)),
            },
            null,
            SelfServiceRecovery: false,
            null);

        PolicyOverride? read = null;

        Settings.OrganizationPolicy
            .Read("a", Settings.OrganizationPolicy.Write(stated))
            .Switch(value => read = value, _ => { });

        Assert.Equal(stated.RequiredAssurance, read?.RequiredAssurance);
        Assert.Equal(stated.SelfServiceRecovery, read?.SelfServiceRecovery);
        Assert.Null(read?.LoginFactors);
        Assert.Null(read?.CredentialRedundancy);
        Assert.Null(read?.EmailDomains);
        Assert.Equal(stated.Gates!.Count, read?.Gates?.Count);
        Assert.All(
            stated.Gates!,
            bound => Assert.Equal(bound.Value, read?.Gates?[bound.Key]));
    }

    /// <summary>
    /// A text the key's type does not write comes back as the failure naming the
    /// key, never as a value and never as a fault.
    /// </summary>
    [Fact]
    public void Read_ATextTheKeyDoesNotWrite_IsTheFailureNamingTheKey()
    {
        ErrorCode? code = null;
        string? key = null;

        Settings.AbuseNonexistentWindow
            .Read("half an hour")
            .Switch(_ => { }, failure =>
            {
                code = failure.Code;
                key = failure.Details["key"].GetString();
            });

        Assert.Equal(ErrorCodes.ConfigurationValueNotAllowed, code);
        Assert.Equal("abuse.nonexistent.window", key);
    }

    /// <summary>
    /// A stored value a tightened ceiling no longer admits comes back as the failure
    /// naming the ceiling, so the caller handles it rather than receiving it.
    /// </summary>
    [Fact]
    public void Read_AStoredValueAboveTheCeiling_IsTheFailureNamingTheCeiling()
    {
        ErrorCode? code = null;

        Settings.SessionAal2Absolute.Read("P2D").Switch(_ => { }, failure => code = failure.Code);

        Assert.Equal(ErrorCodes.ConfigurationValueAboveCeiling, code);
    }

    /// <summary>
    /// A restriction written with no bucket is refused, which is what the key admits
    /// rather than what the text is.
    /// </summary>
    [Fact]
    public void Read_ARestrictionWithNoBucket_IsRefused()
    {
        ErrorCode? code = null;

        Settings.Restrictions
            .Read("[{\"name\":\"sms.destination\",\"key\":\"destination\",\"purpose\":\"any\",\"buckets\":[]}]")
            .Switch(_ => { }, failure => code = failure.Code);

        Assert.Equal(ErrorCodes.ConfigurationValueNotAllowed, code);
    }

    private static TValue? Read<TValue>(Setting<TValue> setting, string stored)
    {
        TValue? held = default;

        setting.Read(stored).Switch(value => held = value, _ => { });

        return held;
    }

    // Every setting knows how to write its own value and read it back; the catalogue
    // holds them as the base type, so the round trip is driven through it.
    private static bool RoundTrips(Setting setting)
    {
        Type type = setting.GetType();

        while (type.BaseType is { } parent && !(parent.IsGenericType
            && parent.GetGenericTypeDefinition() == typeof(Setting<>)))
        {
            type = parent;
        }

        Type value = type.BaseType!.GetGenericArguments()[0];

        return (bool)typeof(SettingWrittenFormTests)
            .GetMethod(nameof(RoundTrip), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(value)
            .Invoke(null, [setting])!;
    }

    // The written form is what the table holds, so writing what was read is what the
    // round trip compares: a value type whose equality is by reference would
    // otherwise report a difference the table does not have.
    private static bool RoundTrip<TValue>(Setting<TValue> setting)
    {
        string written = setting.Write(setting.Default);
        string? again = null;

        setting.Read(written).Switch(value => again = setting.Write(value), _ => { });

        if (again is null)
        {
            return false;
        }

        Assert.Equal(written, again);

        return true;
    }
}
