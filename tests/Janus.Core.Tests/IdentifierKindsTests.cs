using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// Which kind of identifier one field's value is (REG-IDENT-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class IdentifierKindsTests
{
    /// <summary>
    /// REG-IDENT-003: a value carrying the sign is an address, wherever the sign is and
    /// whatever surrounds it, so an address is never read as anything else.
    /// </summary>
    /// <param name="entered">The identifier as it was entered.</param>
    [Theory]
    [InlineData("ahmed@example.com")]
    [InlineData("  ahmed@example.com  ")]
    [InlineData("\u0645\u062D\u0645\u062F@example.com")]
    [InlineData("01001234567@example.com")]
    public void REG_IDENT_003_AValueCarryingTheSignIsAnEmail(string entered) =>
        Assert.Equal(IdentifierKind.Email, IdentifierKinds.Detect(entered, usernamesEnabled: true));

    /// <summary>
    /// REG-IDENT-003: a value that is digits once the separators a person writes are
    /// taken out is a number, in whichever script the digits were typed and with either
    /// prefix an international number carries.
    /// </summary>
    /// <param name="entered">The identifier as it was entered.</param>
    [Theory]
    [InlineData("01001234567")]
    [InlineData("+20 100 123 4567")]
    [InlineData("00201001234567")]
    [InlineData("(02) 1234-5678")]
    [InlineData("  +20.100.123.4567  ")]
    [InlineData("\u0660\u0661\u0660\u0660\u0661\u0662\u0663\u0664\u0665\u0666\u0667")]
    [InlineData("+\u0662\u0660 \u0661\u0660\u0660")]
    public void REG_IDENT_003_AValueThatIsDigitsIsAPhone(string entered) =>
        Assert.Equal(IdentifierKind.Phone, IdentifierKinds.Detect(entered, usernamesEnabled: true));

    /// <summary>
    /// REG-IDENT-003: anything else is a username where the deployment admits one.
    /// </summary>
    /// <param name="entered">The identifier as it was entered.</param>
    [Theory]
    [InlineData("ahmed")]
    [InlineData("a01001234567")]
    [InlineData("\u0645\u062D\u0645\u062F")]
    [InlineData("  ahmed  ")]
    public void REG_IDENT_003_AnythingElseIsAUsername(string entered) =>
        Assert.Equal(IdentifierKind.Username, IdentifierKinds.Detect(entered, usernamesEnabled: true));

    /// <summary>
    /// REG-IDENT-003: where the deployment admits no username, a value of no other kind
    /// is an identifier of no kind and takes the concealed path, rather than being read
    /// as a username the deployment does not hold.
    /// </summary>
    [Fact]
    public void REG_IDENT_003_WhereUsernamesAreOffAnythingElseHasNoKind()
    {
        Assert.Null(IdentifierKinds.Detect("ahmed", usernamesEnabled: false));
        Assert.Equal(
            IdentifierKind.Email,
            IdentifierKinds.Detect("ahmed@example.com", usernamesEnabled: false));
        Assert.Equal(
            IdentifierKind.Phone,
            IdentifierKinds.Detect("01001234567", usernamesEnabled: false));
    }

    /// <summary>
    /// REG-IDENT-003: no value is both a phone number and a username, because a
    /// username holds a letter and a phone number holds none (REG-IDENT-009).
    /// </summary>
    /// <param name="entered">The identifier as it was entered.</param>
    [Theory]
    [InlineData("01001234567")]
    [InlineData("+20 100 123 4567")]
    [InlineData("\u0660\u0661\u0660\u0660\u0661")]
    public void REG_IDENT_003_NoPhoneNumberIsAlsoAUsername(string entered)
    {
        Assert.Equal(IdentifierKind.Phone, IdentifierKinds.Detect(entered, usernamesEnabled: true));
        Assert.False(Username.TryParse(entered, out _));
    }

    /// <summary>
    /// A value of separators alone is not a number, there being no digit left once they
    /// are taken out.
    /// </summary>
    /// <param name="entered">The identifier as it was entered.</param>
    [Theory]
    [InlineData("+")]
    [InlineData("00")]
    [InlineData("()")]
    [InlineData("--")]
    public void Detect_SeparatorsAlone_IsNotAPhone(string entered) =>
        Assert.NotEqual(IdentifierKind.Phone, IdentifierKinds.Detect(entered, usernamesEnabled: true));

    /// <summary>
    /// An absent value is a programming fault, not an identifier of no kind.
    /// </summary>
    [Fact]
    public void Detect_AbsentValue_Throws() =>
        Assert.Throws<ArgumentNullException>(() => IdentifierKinds.Detect(null!, usernamesEnabled: true));
}
