using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Where a registration has reached. The order is fixed and there is no way back: a
/// correction is made in place, never by returning to a completed step.
/// </summary>
/// <remarks>
/// Implements chapter 10 section 5.19, REG-SESS-002 and D-146. The account exists from
/// the end of <see cref="Terms"/>; the steps after it run on the account application in
/// the new session.
/// </remarks>
public enum RegistrationStep
{
    /// <summary>
    /// The neutral age screen, which precedes every identifier field.
    /// </summary>
    [JsonStringEnumMemberName("age")]
    Age = 0,

    /// <summary>
    /// An email address is entered, or a provider supplies one.
    /// </summary>
    [JsonStringEnumMemberName("email")]
    Email = 1,

    /// <summary>
    /// A telephone number is entered, where the deployment asks for one.
    /// </summary>
    [JsonStringEnumMemberName("phone")]
    Phone = 2,

    /// <summary>
    /// Every identifier on one screen, each verified before the step completes.
    /// </summary>
    [JsonStringEnumMemberName("confirm")]
    Confirm = 3,

    /// <summary>
    /// Sign-in methods and the second step on one screen.
    /// </summary>
    [JsonStringEnumMemberName("security")]
    Security = 4,

    /// <summary>
    /// Terms, the notice, the affirmation and the consent controls. The account is
    /// created here.
    /// </summary>
    [JsonStringEnumMemberName("terms")]
    Terms = 5,

    /// <summary>
    /// The profile fields whose policies are on, and the username where enabled.
    /// </summary>
    [JsonStringEnumMemberName("about")]
    About = 6,

    /// <summary>
    /// The person-editable preference values.
    /// </summary>
    [JsonStringEnumMemberName("preferences")]
    Preferences = 7,

    /// <summary>
    /// The invitation acknowledgement, where an invitation brought the person.
    /// </summary>
    [JsonStringEnumMemberName("membership")]
    Membership = 8,

    /// <summary>
    /// The summary and the return to the client's registered address.
    /// </summary>
    [JsonStringEnumMemberName("done")]
    Done = 9,
}
