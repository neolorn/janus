using System;
using Janus.Authentication.Sending;
using Xunit;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// What one text message holds, which is a question about the language and not
/// about the template: a message one character outside the default alphabet costs a
/// second message seventy characters in (AUTH-ABUSE-005, INT-SMS-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class MessageBudgetTests
{
    /// <summary>
    /// AUTH-ABUSE-005 AC1 and INT-SMS-003 AC1: a Latin message holds a hundred and
    /// sixty characters, and one character more costs a second message.
    /// </summary>
    [Fact]
    public void AUTH_ABUSE_005_AC1_ALatinMessageHoldsOneHundredAndSixty()
    {
        Assert.Equal(160, MessageBudget.Of("your code is 429184"));
        Assert.False(MessageBudget.Exceeds(new string('a', 160)));
        Assert.True(MessageBudget.Exceeds(new string('a', 161)));
    }

    /// <summary>
    /// AUTH-ABUSE-005 AC1 and INT-SMS-003 AC1, with the Arabic binding the chapter
    /// names: a message outside the default alphabet holds seventy.
    /// </summary>
    [Fact]
    public void AUTH_ABUSE_005_AC1_AnArabicMessageHoldsSeventy()
    {
        const string arabic = "رمز التحقق الخاص بك هو 429184";

        Assert.Equal(70, MessageBudget.Of(arabic));
        Assert.False(MessageBudget.Exceeds(arabic));
        Assert.True(MessageBudget.Exceeds(new string('م', 71)));
        Assert.False(MessageBudget.Exceeds(new string('م', 70)));
    }

    /// <summary>
    /// A character of the GSM extension table costs two units, so a message of
    /// eighty-one braces is over the Latin budget although it is eighty-one
    /// characters long (AUTH-ABUSE-005).
    /// </summary>
    [Fact]
    public void Units_ACharacterOfTheExtensionTable_CostsTwo()
    {
        Assert.Equal(2, MessageBudget.Units("{"));
        Assert.Equal(2, MessageBudget.Units("\\"));
        Assert.Equal(1, MessageBudget.Units("a"));

        Assert.True(MessageBudget.Exceeds(new string('{', 81)));
        Assert.False(MessageBudget.Exceeds(new string('{', 80)));
    }

    /// <summary>
    /// INT-SMS-003: a text that carries a link holds two segments of its alphabet, three
    /// hundred and six units in the default one and a hundred and thirty-four outside it.
    /// </summary>
    [Fact]
    public void INT_SMS_003_ATextCarryingALinkHoldsTwoSegments()
    {
        Assert.Equal(306, MessageBudget.Of("follow https://a.example.test/link#x.y", linked: true));
        Assert.False(MessageBudget.Exceeds(new string('a', 306), linked: true));
        Assert.True(MessageBudget.Exceeds(new string('a', 307), linked: true));
        Assert.Equal(134, MessageBudget.Of("م", linked: true));
        Assert.False(MessageBudget.Exceeds(new string('م', 134), linked: true));
        Assert.True(MessageBudget.Exceeds(new string('م', 135), linked: true));
    }

    /// <summary>
    /// One character outside the default alphabet moves the whole message to the
    /// narrow budget, which is the doubling the chapter warns of (AUTH-ABUSE-005).
    /// </summary>
    [Fact]
    public void Of_OneCharacterOutsideTheDefaultAlphabet_NarrowsTheWholeMessage()
    {
        Assert.Equal(160, MessageBudget.Of(new string('a', 100)));
        Assert.Equal(70, MessageBudget.Of(new string('a', 99) + "م"));
        Assert.True(MessageBudget.Exceeds(new string('a', 99) + "م"));
    }

    /// <summary>
    /// The budget is asked of the text and of nothing else, so an empty template is
    /// within it (AUTH-ABUSE-005).
    /// </summary>
    [Fact]
    public void Exceeds_AnEmptyTemplate_IsWithinBudget()
    {
        Assert.False(MessageBudget.Exceeds(string.Empty));
        Assert.Throws<ArgumentNullException>(() => MessageBudget.Exceeds(null!));
    }
}
