using System.Text.Json.Serialization;

namespace Janus.Authentication.Sending;

/// <summary>
/// What a link the library sends is for, which decides the application it lands on and
/// what the landing component does with its token once the person presses.
/// </summary>
/// <remarks>
/// Implements API-LAND-001 and FE-VER-001, chapter 10 section 5.43. The set is closed.
/// The first five land on the authentication application, the rest on the account
/// application.
/// </remarks>
internal enum LinkKind
{
    /// <summary>A link that signs the person in.</summary>
    [JsonStringEnumMemberName("sign-in")]
    SignIn,

    /// <summary>A link that proves an address being registered.</summary>
    [JsonStringEnumMemberName("registration")]
    Registration,

    /// <summary>A link that sets a new password.</summary>
    [JsonStringEnumMemberName("recovery")]
    Recovery,

    /// <summary>A link that carries an admin-assisted enrolment.</summary>
    [JsonStringEnumMemberName("enrolment")]
    Enrolment,

    /// <summary>A link that opens an invitation into an organization.</summary>
    [JsonStringEnumMemberName("invitation")]
    Invitation,

    /// <summary>A link that proves an address being added to or replaced on an account.</summary>
    [JsonStringEnumMemberName("identifier")]
    Identifier,

    /// <summary>A link that confirms an address may be replaced.</summary>
    [JsonStringEnumMemberName("identifier-confirm")]
    IdentifierConfirm,

    /// <summary>A link that undoes an identifier's removal.</summary>
    [JsonStringEnumMemberName("undo")]
    Undo,

    /// <summary>A link that cancels the account's deletion.</summary>
    [JsonStringEnumMemberName("deletion-cancel")]
    DeletionCancel,

    /// <summary>A link that stands a deactivated account back up.</summary>
    [JsonStringEnumMemberName("reactivation")]
    Reactivation,

    /// <summary>A link that cancels a loss report.</summary>
    [JsonStringEnumMemberName("loss-report")]
    LossReport,
}
