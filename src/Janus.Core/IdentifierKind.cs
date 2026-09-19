using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// The three kinds of identifier an account holds.
/// </summary>
/// <remarks>
/// Implements REG-IDENT-001 and chapter 10 section 5.17. Email is always enabled and an
/// account always holds at least one verified one; phone is required by default and is
/// never an account's sole identifier; username is disabled by default and absent from
/// every screen and response while it is.
/// </remarks>
public enum IdentifierKind
{
    /// <summary>
    /// An email address. The anchor an account cannot lose.
    /// </summary>
    [JsonStringEnumMemberName("email")]
    Email = 0,

    /// <summary>
    /// A telephone number, held as E.164. The registration anti-abuse control and a
    /// recovery channel.
    /// </summary>
    [JsonStringEnumMemberName("phone")]
    Phone = 1,

    /// <summary>
    /// A username. Public by nature, and the one identifier whose existence is
    /// disclosed.
    /// </summary>
    [JsonStringEnumMemberName("username")]
    Username = 2,
}
