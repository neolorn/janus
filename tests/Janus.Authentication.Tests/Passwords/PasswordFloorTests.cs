using System.Text;
using Janus.Authentication.Passwords;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Passwords;

/// <summary>
/// The length rule (AUTH-PASS-001, AUTH-PASS-001a, AUTH-PASS-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class PasswordFloorTests
{
    private static readonly int SingleFactor = Settings.PasswordFloorSingleFactor.Default;
    private static readonly int WithMfa = Settings.PasswordFloorWithMfa.Default;
    private static readonly int Maximum = Settings.PasswordMaximum.Default;

    /// <summary>
    /// AUTH-PASS-001 AC1: fourteen characters is short of the floor that applies
    /// where the password could sign in alone.
    /// </summary>
    [Fact]
    public void AUTH_PASS_001_AC1_AFourteenCharacterPasswordIsRefusedForASingleFactorAccount() =>
        Assert.Equal(ErrorCodes.PasswordTooShort, Refusal(Admits(14, AssuranceLevel.Aal1)));

    /// <summary>
    /// AUTH-PASS-001 AC1: fifteen characters meets it.
    /// </summary>
    [Fact]
    public void AUTH_PASS_001_AC1_AFifteenCharacterPasswordIsAcceptedForASingleFactorAccount() =>
        Assert.Null(Refusal(Admits(15, AssuranceLevel.Aal1)));

    /// <summary>
    /// AUTH-PASS-001 AC2: ten characters is accepted on an account that reaches AAL2,
    /// where the password can never complete a sign-in by itself.
    /// </summary>
    [Fact]
    public void AUTH_PASS_001_AC2_ATenCharacterPasswordIsAcceptedWhereASecondFactorIsHeld() =>
        Assert.Null(Refusal(Admits(10, AssuranceLevel.Aal2)));

    /// <summary>
    /// AUTH-PASS-001a AC2: the same ten characters on an account that reaches only
    /// AAL1 is refused, the second factor being the price of the shorter floor.
    /// </summary>
    [Fact]
    public void AUTH_PASS_001a_AC2_ATenCharacterPasswordIsRefusedWhereNoSecondFactorIsHeld() =>
        Assert.Equal(ErrorCodes.PasswordTooShort, Refusal(Admits(10, AssuranceLevel.Aal1)));

    /// <summary>
    /// AUTH-PASS-001 AC3: the maximum is accepted and one character beyond it is
    /// refused rather than truncated.
    /// </summary>
    [Fact]
    public void AUTH_PASS_001_AC3_TheMaximumIsAcceptedAndOneBeyondItIsRefused()
    {
        Assert.Null(Refusal(Admits(Maximum, AssuranceLevel.Aal1)));
        Assert.Equal(
            ErrorCodes.ConfigurationValueAboveCeiling,
            Refusal(Admits(Maximum + 1, AssuranceLevel.Aal1)));
    }

    /// <summary>
    /// AUTH-PASS-001 AC3: a password is as long as its characters, whatever they cost
    /// to encode, so nothing is truncated by how it was written.
    /// </summary>
    [Fact]
    public void AUTH_PASS_001_AC3_LengthIsCountedInCharactersAndNotInBytes()
    {
        byte[] astral = Encoding.UTF8.GetBytes(string.Concat(Enumerable128("\U0001F600")));

        Assert.Equal(128, PasswordFloor.Characters(astral));
        Assert.Null(Refusal(Admits(PasswordFloor.Characters(astral), AssuranceLevel.Aal1)));
    }

    /// <summary>
    /// AUTH-PASS-002 AC1: a fifteen-character all-lowercase password meets the rule,
    /// there being no character-class requirement to miss.
    /// </summary>
    [Fact]
    public void AUTH_PASS_002_AC1_AnAllLowercasePasswordMeetingTheFloorIsAccepted()
    {
        byte[] password = Encoding.UTF8.GetBytes("orangemarmalade");

        Assert.Null(Refusal(Admits(PasswordFloor.Characters(password), AssuranceLevel.Aal1)));
    }

    /// <summary>
    /// AUTH-PASS-001a AC3: whether the password stands on its own is decided by the
    /// single-factor floor alone.
    /// </summary>
    [Fact]
    public void AUTH_PASS_001a_AC3_TheFlagFollowsTheSingleFactorFloor()
    {
        Assert.True(PasswordFloor.MeetsSingleFactorFloor(SingleFactor, SingleFactor));
        Assert.False(PasswordFloor.MeetsSingleFactorFloor(SingleFactor - 1, SingleFactor));
    }

    private static string[] Enumerable128(string glyph)
    {
        string[] glyphs = new string[128];

        for (int at = 0; at < glyphs.Length; at++)
        {
            glyphs[at] = glyph;
        }

        return glyphs;
    }

    private static Result Admits(int characters, AssuranceLevel reachable) =>
        PasswordFloor.Admits(characters, SingleFactor, WithMfa, Maximum, reachable);

    private static ErrorCode? Refusal(Result admitted) =>
        admitted.Match<ErrorCode?>(() => null, error => error.Code);
}
