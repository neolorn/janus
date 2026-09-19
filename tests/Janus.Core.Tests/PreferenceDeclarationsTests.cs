using System.Collections.Generic;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The preference keys a host declares at startup (REG-PREF-001).
/// </summary>
/// <remarks>
/// A declaration is read once, when the deployment starts, so that a host that has
/// spelled one wrong learns of it then and not from the first person who sets it.
/// </remarks>
[Trait("kind", "unit")]
public sealed class PreferenceDeclarationsTests
{
    /// <summary>
    /// REG-PREF-001 AC1: a declaration without a name is malformed, and the fault
    /// carries the code chapter 10 names for it.
    /// </summary>
    [Fact]
    public void REG_PREF_001_AC1_ADeclarationWithoutANameFailsStartup()
    {
        StartupException fault = Assert.Throws<StartupException>(() =>
            PreferenceDeclarations.Of([new PreferenceDeclaration(" ", PreferenceKind.Text, "dark")]));

        Assert.Equal(ErrorCodes.StartupPreferenceDeclaration, fault.Failure?.Code);
    }

    /// <summary>
    /// REG-PREF-001 AC1: one key declared twice is malformed, because the second
    /// declaration would silently govern.
    /// </summary>
    [Fact]
    public void REG_PREF_001_AC1_AKeyDeclaredTwiceFailsStartup()
    {
        StartupException fault = Assert.Throws<StartupException>(() =>
            PreferenceDeclarations.Of(
            [
                new PreferenceDeclaration("theme", PreferenceKind.Text, "dark"),
                new PreferenceDeclaration("theme", PreferenceKind.Text, "light"),
            ]));

        Assert.Equal(ErrorCodes.StartupPreferenceDeclaration, fault.Failure?.Code);
    }

    /// <summary>
    /// REG-PREF-001 AC1: a choice that names no value admits nothing, so no person
    /// could ever set it.
    /// </summary>
    [Fact]
    public void REG_PREF_001_AC1_AChoiceThatNamesNoValueFailsStartup()
    {
        StartupException fault = Assert.Throws<StartupException>(() =>
            PreferenceDeclarations.Of(
                [new PreferenceDeclaration("theme", PreferenceKind.Choice, "dark")]));

        Assert.Equal(ErrorCodes.StartupPreferenceDeclaration, fault.Failure?.Code);
    }

    /// <summary>
    /// REG-PREF-001 AC1: values named on a key whose type does not take them are a
    /// declaration the host did not mean.
    /// </summary>
    [Fact]
    public void REG_PREF_001_AC1_ValuesOnAKindThatTakesNoneFailStartup()
    {
        StartupException fault = Assert.Throws<StartupException>(() =>
            PreferenceDeclarations.Of(
            [
                new PreferenceDeclaration(
                    "theme",
                    PreferenceKind.Text,
                    "dark",
                    Choices: new HashSet<string> { "dark", "light" }),
            ]));

        Assert.Equal(ErrorCodes.StartupPreferenceDeclaration, fault.Failure?.Code);
    }

    /// <summary>
    /// REG-PREF-001 AC1: a default its own type refuses would be in force for every
    /// account that has set nothing.
    /// </summary>
    [Fact]
    public void REG_PREF_001_AC1_ADefaultTheKindRefusesFailsStartup()
    {
        StartupException fault = Assert.Throws<StartupException>(() =>
            PreferenceDeclarations.Of(
                [new PreferenceDeclaration("reduced-motion", PreferenceKind.Flag, "yes")]));

        Assert.Equal(ErrorCodes.StartupPreferenceDeclaration, fault.Failure?.Code);
    }

    /// <summary>
    /// The fault names the key it was found on, so an operator reading a startup
    /// failure knows which declaration to correct.
    /// </summary>
    [Fact]
    public void Of_AMalformedDeclaration_NamesTheKeyInTheFailure()
    {
        StartupException fault = Assert.Throws<StartupException>(() =>
            PreferenceDeclarations.Of(
                [new PreferenceDeclaration("text-size", PreferenceKind.Number, "large")]));

        Assert.Equal("text-size", fault.Failure?.Details["preference"].GetString());
    }

    /// <summary>
    /// A host that declares nothing starts, and finds nothing.
    /// </summary>
    [Fact]
    public void Of_NoDeclarations_YieldsAnEmptySet()
    {
        var declarations = PreferenceDeclarations.Of([]);

        Assert.Empty(declarations.All);
        Assert.False(declarations.TryFind("theme", out _));
    }

    /// <summary>
    /// Every well-formed kind is declarable, and each is found under the name the host
    /// gave it.
    /// </summary>
    [Fact]
    public void Of_EveryKind_IsFoundUnderItsName()
    {
        var declarations = PreferenceDeclarations.Of(
        [
            new PreferenceDeclaration("date-format", PreferenceKind.Text, "iso"),
            new PreferenceDeclaration("reduced-motion", PreferenceKind.Flag, "false"),
            new PreferenceDeclaration("text-size", PreferenceKind.Number, "16"),
            new PreferenceDeclaration(
                "theme",
                PreferenceKind.Choice,
                "dark",
                Choices: new HashSet<string> { "dark", "light" }),
        ]);

        Assert.Equal(4, declarations.All.Count);
        Assert.True(declarations.TryFind("theme", out PreferenceDeclaration theme));
        Assert.Equal(PreferenceKind.Choice, theme.Kind);
    }

    /// <summary>
    /// A key the host did not declare is not found, whatever it is spelled like.
    /// </summary>
    [Fact]
    public void TryFind_AnUndeclaredKey_IsNotFound()
    {
        var declarations = PreferenceDeclarations.Of(
            [new PreferenceDeclaration("theme", PreferenceKind.Text, "dark")]);

        Assert.False(declarations.TryFind("Theme", out _));
    }

    /// <summary>
    /// What a key admits is its declared type's business and nothing else's: a flag
    /// takes the two spellings the wire uses and a number takes an integer.
    /// </summary>
    /// <param name="kind">The declared type.</param>
    /// <param name="value">The value as it was sent.</param>
    /// <param name="admitted">Whether the type takes it.</param>
    [Theory]
    [InlineData(PreferenceKind.Flag, "true", true)]
    [InlineData(PreferenceKind.Flag, "false", true)]
    [InlineData(PreferenceKind.Flag, "True", false)]
    [InlineData(PreferenceKind.Flag, "1", false)]
    [InlineData(PreferenceKind.Number, "16", true)]
    [InlineData(PreferenceKind.Number, "-16", true)]
    [InlineData(PreferenceKind.Number, "16.5", false)]
    [InlineData(PreferenceKind.Number, "", false)]
    [InlineData(PreferenceKind.Text, "anything at all", true)]
    public void Admits_AValueOfAKind_AnswersForTheKind(PreferenceKind kind, string value, bool admitted)
    {
        var declaration = new PreferenceDeclaration("key", kind, kind is PreferenceKind.Flag ? "false" : "0");

        Assert.Equal(admitted, declaration.Admits(value));
    }

    /// <summary>
    /// A choice admits the values it named and no others.
    /// </summary>
    [Fact]
    public void Admits_AChoice_TakesOnlyTheValuesItNamed()
    {
        var declaration = new PreferenceDeclaration(
            "theme",
            PreferenceKind.Choice,
            "dark",
            Choices: new HashSet<string> { "dark", "light" });

        Assert.True(declaration.Admits("light"));
        Assert.False(declaration.Admits("sepia"));
    }
}
