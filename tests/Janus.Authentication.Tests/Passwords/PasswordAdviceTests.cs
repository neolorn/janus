using System.Text;
using Janus.Authentication.Passwords;
using Xunit;

namespace Janus.Authentication.Tests.Passwords;

/// <summary>
/// Advisory feedback, which refuses nothing (AUTH-PASS-005).
/// </summary>
[Trait("kind", "unit")]
public sealed class PasswordAdviceTests
{
    /// <summary>
    /// AUTH-PASS-005 AC1: a password a heuristic scores poorly is still a password;
    /// the feedback says so and nothing refuses it.
    /// </summary>
    [Fact]
    public void AUTH_PASS_005_AC1_APoorlyScoringPasswordIsReportedAndNotRefused()
    {
        PasswordFeedback feedback = PasswordAdvice.On(Encoding.UTF8.GetBytes("aaaaaaaaaaaaaaa"), []);

        Assert.True(feedback.Weak);
        Assert.Empty(feedback.OwnWords);
    }

    /// <summary>
    /// AUTH-PASS-005 AC2: the feedback names the person's own word and the service
    /// name where the password holds one.
    /// </summary>
    [Fact]
    public void AUTH_PASS_005_AC2_TheFeedbackNamesTheOwnWordThePasswordHolds()
    {
        PasswordFeedback feedback = PasswordAdvice.On(
            Encoding.UTF8.GetBytes("hossam at orangemarmalade"),
            ["hossam", "orangemarmalade", "unrelated"]);

        Assert.Equal(["hossam", "orangemarmalade"], feedback.OwnWords);
        Assert.False(feedback.Weak);
    }

    /// <summary>
    /// AUTH-PASS-005: the person's own words are matched without regard to case, as
    /// the feedback would be useless otherwise.
    /// </summary>
    [Fact]
    public void On_AnOwnWordInAnotherCase_IsStillNamed()
    {
        PasswordFeedback feedback = PasswordAdvice.On(
            Encoding.UTF8.GetBytes("HOSSAM at the seaside"),
            ["hossam"]);

        Assert.Equal(["hossam"], feedback.OwnWords);
    }
}
