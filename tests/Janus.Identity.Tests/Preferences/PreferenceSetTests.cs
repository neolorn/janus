using System;
using System.Collections.Generic;
using Janus.Core;
using Janus.Identity.Preferences;
using Xunit;

namespace Janus.Identity.Tests.Preferences;

/// <summary>
/// What an account has settled: the language, the time zone and the values of the keys
/// the host declared (IDN-ATTR-001, REG-PREF-001).
/// </summary>
/// <remarks>
/// Every refusal here is about the shape of a value and never about its meaning, which
/// is the host's: the library validates against the declaration and stops there.
/// </remarks>
[Trait("kind", "unit")]
public sealed class PreferenceSetTests
{
    private const int Maximum = 8192;

    private static readonly SubjectId Subject =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private static readonly PreferenceDeclarations Declared = PreferenceDeclarations.Of(
    [
        new PreferenceDeclaration(
            "theme",
            PreferenceKind.Enum,
            "dark",
            Choices: new HashSet<string> { "dark", "light" }),
        new PreferenceDeclaration("text-size", PreferenceKind.Integer, "16"),
        new PreferenceDeclaration("motto", PreferenceKind.String, string.Empty),
        new PreferenceDeclaration("beta-features", PreferenceKind.Boolean, "false", AdministratorOnly: true),
    ]);

    /// <summary>
    /// REG-PREF-001 AC2: a key the host never declared is refused, whatever its value
    /// looks like.
    /// </summary>
    [Fact]
    public void REG_PREF_001_AC2_AnUndeclaredKeyIsRefused()
    {
        var preferences = PreferenceSet.Empty(Subject);

        Result outcome = preferences.Set(Declared, "colour", "blue", asAdministrator: false, Maximum);

        Assert.Equal(ErrorCodes.PreferenceUndeclared, Code(outcome));
        Assert.Empty(preferences.Values);
    }

    /// <summary>
    /// REG-PREF-001 AC2: a value of a type other than the declared one is refused.
    /// </summary>
    /// <param name="name">The declared key.</param>
    /// <param name="value">A value its type does not take.</param>
    [Theory]
    [InlineData("text-size", "large")]
    [InlineData("theme", "sepia")]
    public void REG_PREF_001_AC2_AValueOfTheWrongTypeIsRefused(string name, string value)
    {
        var preferences = PreferenceSet.Empty(Subject);

        Result outcome = preferences.Set(Declared, name, value, asAdministrator: false, Maximum);

        Assert.Equal(ErrorCodes.PreferenceWrongType, Code(outcome));
        Assert.Empty(preferences.Values);
    }

    /// <summary>
    /// REG-PREF-001 AC2: a set that would pass <c>preferences.maxsize</c> is refused,
    /// and the value that would have carried it over is not held.
    /// </summary>
    [Fact]
    public void REG_PREF_001_AC2_ASetOverTheMaximumIsRefused()
    {
        var preferences = PreferenceSet.Empty(Subject);

        Result outcome = preferences.Set(
            Declared,
            "motto",
            new string('a', Maximum),
            asAdministrator: false,
            Maximum);

        Assert.Equal(ErrorCodes.PreferenceTooLarge, Code(outcome));
        Assert.Empty(preferences.Values);
    }

    /// <summary>
    /// REG-PREF-001 AC2: a refusal for size leaves the value the account had, rather
    /// than the one that took the set over.
    /// </summary>
    [Fact]
    public void REG_PREF_001_AC2_ASetOverTheMaximumKeepsTheValueItHeld()
    {
        var preferences = PreferenceSet.Empty(Subject);
        Assert.Null(Code(preferences.Set(Declared, "motto", "brief", asAdministrator: false, Maximum)));

        Result outcome = preferences.Set(
            Declared,
            "motto",
            new string('a', Maximum),
            asAdministrator: false,
            Maximum);

        Assert.Equal(ErrorCodes.PreferenceTooLarge, Code(outcome));
        Assert.Equal("brief", preferences.Values["motto"]);
    }

    /// <summary>
    /// REG-PREF-001 AC3: a preference declared administrator-only is refused when the
    /// person sets it.
    /// </summary>
    [Fact]
    public void REG_PREF_001_AC3_AnAdministratorOnlyPreferenceIsRefusedFromThePerson()
    {
        var preferences = PreferenceSet.Empty(Subject);

        Result outcome = preferences.Set(
            Declared,
            "beta-features",
            "true",
            asAdministrator: false,
            Maximum);

        Assert.Equal(ErrorCodes.PreferenceAdministratorOnly, Code(outcome));
        Assert.Empty(preferences.Values);
    }

    /// <summary>
    /// REG-PREF-001 AC3: the same preference is set when an administrator sets it.
    /// </summary>
    [Fact]
    public void REG_PREF_001_AC3_AnAdministratorOnlyPreferenceIsSetByAnAdministrator()
    {
        var preferences = PreferenceSet.Empty(Subject);

        Result outcome = preferences.Set(
            Declared,
            "beta-features",
            "true",
            asAdministrator: true,
            Maximum);

        Assert.Null(Code(outcome));
        Assert.Equal("true", preferences.Values["beta-features"]);
    }

    /// <summary>
    /// An undeclared key is refused before anything else is weighed, so the refusal
    /// never tells a caller that a key it may not set exists.
    /// </summary>
    [Fact]
    public void Set_AnUndeclaredKeyOfTheWrongType_IsRefusedAsUndeclared()
    {
        var preferences = PreferenceSet.Empty(Subject);

        Result outcome = preferences.Set(Declared, "colour", string.Empty, asAdministrator: true, Maximum);

        Assert.Equal(ErrorCodes.PreferenceUndeclared, Code(outcome));
    }

    /// <summary>
    /// The value in force is the account's where it set one and the host's default
    /// where it did not.
    /// </summary>
    [Fact]
    public void InForce_AKeyTheAccountNeverSet_IsTheDeclaredDefault()
    {
        var preferences = PreferenceSet.Empty(Subject);
        Assert.True(Declared.TryFind("theme", out PreferenceDeclaration theme));

        Assert.Equal("dark", preferences.InForce(theme));
        Assert.Null(Code(preferences.Set(Declared, "theme", "light", asAdministrator: false, Maximum)));
        Assert.Equal("light", preferences.InForce(theme));
    }

    /// <summary>
    /// A value given up returns its key to the host's default, and leaves nothing in
    /// the set to be written or exported.
    /// </summary>
    [Fact]
    public void Clear_AValueTheAccountSet_ReturnsTheKeyToItsDefault()
    {
        var preferences = PreferenceSet.Empty(Subject);
        Assert.True(Declared.TryFind("theme", out PreferenceDeclaration theme));
        Assert.Null(Code(preferences.Set(Declared, "theme", "light", asAdministrator: false, Maximum)));

        preferences.Clear("theme");

        Assert.Empty(preferences.Values);
        Assert.Equal("dark", preferences.InForce(theme));
    }

    /// <summary>
    /// The size the maximum bounds is the set's keys and values as UTF-8 bytes, so a
    /// value outside the Basic Latin block counts what it costs to store.
    /// </summary>
    [Fact]
    public void Size_AValueOutsideBasicLatin_CountsItsBytes()
    {
        var preferences = PreferenceSet.Empty(Subject);
        Assert.Null(Code(preferences.Set(Declared, "motto", "مرحبا", asAdministrator: false, Maximum)));

        Assert.Equal("motto".Length + 10, preferences.Size);
    }

    /// <summary>
    /// The language and the time zone are the library's own and are not declared keys:
    /// they are set and cleared without a declaration and count towards no maximum.
    /// </summary>
    [Fact]
    public void SetLanguage_AndTimeZone_AreHeldWithoutADeclaration()
    {
        var preferences = PreferenceSet.Empty(Subject);

        preferences.SetLanguage("ar-EG");
        preferences.SetTimeZone("Africa/Cairo");

        Assert.Equal("ar-EG", preferences.Language);
        Assert.Equal("Africa/Cairo", preferences.TimeZone);
        Assert.Equal(0, preferences.Size);

        preferences.SetLanguage(null);

        Assert.Null(preferences.Language);
    }

    /// <summary>
    /// A set read back from a row carries what was stored and is no change the account
    /// made.
    /// </summary>
    [Fact]
    public void Existing_AStoredRow_CarriesWhatWasStored()
    {
        var preferences = PreferenceSet.Existing(
            Subject,
            "ar-EG",
            "Africa/Cairo",
            new Dictionary<string, string> { ["theme"] = "light" });

        Assert.Equal(Subject, preferences.Subject);
        Assert.Equal("ar-EG", preferences.Language);
        Assert.Equal("Africa/Cairo", preferences.TimeZone);
        Assert.Equal("light", preferences.Values["theme"]);
    }

    private static ErrorCode? Code(Result outcome)
    {
        ErrorCode? code = null;

        outcome.Switch(() => { }, failure => code = failure.Code);

        return code;
    }
}
