using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// The catalogue is the one place a factor is named (AUTH-FACT-001, AUTH-FACT-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class FactorCatalogueTests
{
    /// <summary>
    /// AUTH-FACT-002: every entry of the catalogue carries what it may do, so a rule
    /// handed an entry always has properties to read.
    /// </summary>
    [Fact]
    public void AUTH_FACT_002_EveryEntryOfTheCatalogueIsRegistered() =>
        Assert.Equal(Enum.GetValues<Factor>().Order(), FactorCatalogue.Entries.Keys.Order());

    /// <summary>
    /// AUTH-FACT-001 AC1: the one conditional in the library that reads a factor by
    /// name is the policy object's refusal of the emergency credential, which chapter
    /// 10 section 4.1a states by name; no rule about what a factor may do reads one.
    /// </summary>
    [Fact]
    public void AUTH_FACT_001_AC1_NoConditionalTestsForAFactorByName() =>
        Assert.Equal([Path.Combine("Janus.Core", "Policy.cs")], BranchingOnAFactor());

    /// <summary>
    /// AUTH-STEP-008 invariants 1 and 2: no rule about a step-up gate names a factor,
    /// and none reads what the account has enrolled by name; the one file that tests
    /// for an entry is the policy object, which chapter 10 section 4.1a states by
    /// name.
    /// </summary>
    [Fact]
    public void AUTH_STEP_008_AC1_NoGateRuleNamesAFactor() =>
        Assert.DoesNotContain(
            Path.Combine("Janus.Authentication", "Factors", "StepUp.cs"),
            BranchingOnAFactor());

    /// <summary>
    /// AUTH-FACT-004 AC1 and AUTH-FACT-002 AC4: a channel verification code is no
    /// entry of the catalogue, so no sign-in path can be handed one: the only thing
    /// a sign-in takes is an entry, and none of them verifies a channel.
    /// </summary>
    [Fact]
    public void AUTH_FACT_004_AC1_AVerificationCodeIsNoCatalogueEntry() =>
        Assert.DoesNotContain(FactorCatalogue.Entries, entry => entry.Value.VerificationOnly);

    /// <summary>
    /// AUTH-FACT-002 AC4: the same fact stated from the sign-in side, the arithmetic
    /// that assigns a tier answering nothing for a verification-only factor.
    /// </summary>
    [Fact]
    public void AUTH_FACT_002_AC4_NoSignInPathAcceptsAVerificationCodeAsACredential()
    {
        var code = new FactorProperties(
            CanBePrimary: true,
            CanBeSecondFactor: true,
            IsPhishingResistant: false,
            AssuranceLevel.Aal2,
            VerificationOnly: true,
            SignInOnly: false,
            IsWebAuthn: false,
            IsDiscoverable: false,
            SingleUse: false);

        Assert.Null(Assurance.Reached([code]));
        Assert.Null(Assurance.Proved([code]));
    }

    /// <summary>
    /// AUTH-FACT-002 AC2a: no key of chapter 10 section 4 turns an entry on or off,
    /// so removing it from the policy's login factors is the only way. The keys that
    /// name an entry set its parameters (a length floor, a drift tolerance) and
    /// admit nothing.
    /// </summary>
    [Fact]
    public void AUTH_FACT_002_AC2a_NoConfigurationKeyEnablesOrDisablesAFactor() =>
        Assert.DoesNotContain(
            Settings.All.OfType<FlagSetting>(),
            setting => Array.Exists(
                Enum.GetNames<Factor>(),
                name => setting.Key.ToString().Contains(name, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// AUTH-PRIN-002 AC1: no enum, flag or claim of the library tells a member of
    /// staff from a customer. The one file that names either is the takedown
    /// trigger, whose values are chapter 10 section 5.12d's vocabulary for what
    /// raised a report, which is not a property of a principal.
    /// </summary>
    [Fact]
    public void AUTH_PRIN_002_AC1_NoEnumFlagOrClaimDistinguishesStaffFromCustomers()
    {
        string[] naming =
        [
            .. Tree()
                .Where(file => File.ReadLines(file).Any(Distinguishes))
                .Select(Relative)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal([Path.Combine("Janus.Core", "TakedownTrigger.cs")], naming);
    }

    // A line of code, comments aside, that names one kind of person.
    private static bool Distinguishes(string line)
    {
        string code = line.TrimStart();

        return !code.StartsWith("//", StringComparison.Ordinal)
            && (code.Contains("Staff", StringComparison.Ordinal)
                || code.Contains("Customer", StringComparison.Ordinal));
    }

    // The files of the library in which a catalogue entry is named inside a construct
    // that chooses between two paths, as the repository lays them out.
    private static string[] BranchingOnAFactor() =>
    [
        .. Tree()
            .Where(file => File.ReadLines(file).Any(Chooses))
            .Select(Relative)
            .Order(StringComparer.Ordinal),
    ];

    // A construct that chooses between two paths.
    private static readonly string[] Branches =
    [
        "if (", "case ", "switch", " is ", "==", "!=", "?",
        "Contains(", "Any(", "All(", "Where(", "Exists(",
    ];

    private static bool Chooses(string line) =>
        line.Contains("Factor.", StringComparison.Ordinal)
        && Array.Exists(Branches, branch => line.Contains(branch, StringComparison.Ordinal));

    private static string[] Tree() => Directory.GetFiles(
        Path.Combine(Root(), "src"),
        "*.cs",
        SearchOption.AllDirectories);

    private static string Relative(string file) =>
        Path.GetRelativePath(Path.Combine(Root(), "src"), file);

    private static string Root()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at is not null && !Directory.Exists(Path.Combine(at.FullName, "src")))
        {
            at = at.Parent;
        }

        return at!.FullName;
    }
}
