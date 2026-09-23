using Janus.Authentication.Sending;
using Xunit;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The order a message's language is resolved in, and what a tag finds among the
/// languages the deployment declares (IDN-ATTR-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class RecipientLanguageTests
{
    private static readonly string[] Declared = ["en", "ar"];

    /// <summary>
    /// IDN-ATTR-001: the stored preference comes first, the request's locale second,
    /// and where there is neither the message goes out in every declared language.
    /// </summary>
    [Fact]
    public void IDN_ATTR_001_ThePreferenceComesBeforeTheRequestAndEitherBeforeEveryLanguage()
    {
        Assert.Equal("ar", RecipientLanguage.Of("ar", "en", Declared));
        Assert.Equal("en", RecipientLanguage.Of(preference: null, "en", Declared));
        Assert.Null(RecipientLanguage.Of(preference: null, requested: null, Declared));
    }

    /// <summary>
    /// IDN-ATTR-001: a tag finds the declared language it narrows, whatever its case;
    /// one that finds none is taken as unset, so the next step answers.
    /// </summary>
    [Fact]
    public void IDN_ATTR_001_ATagFindsTheDeclaredLanguageItNarrows()
    {
        Assert.Equal("en", RecipientLanguage.Found("EN-gb", Declared));
        Assert.Equal("ar", RecipientLanguage.Found("ar-EG-u-nu-latn", Declared));
        Assert.Equal("en", RecipientLanguage.Found("en-x-private", Declared));
        Assert.Null(RecipientLanguage.Found("fr-FR", Declared));
        Assert.Null(RecipientLanguage.Found("*", Declared));
        Assert.Null(RecipientLanguage.Found(" ", Declared));
        Assert.Equal("ar", RecipientLanguage.Of("fr", "ar-SA", Declared));
    }
}
