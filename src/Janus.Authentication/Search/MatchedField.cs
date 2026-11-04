namespace Janus.Authentication.Search;

/// <summary>
/// Which of a subject's fields a search matched first, in the order name, email,
/// phone.
/// </summary>
/// <remarks>Implements IDN-SRCH-001 (D-256).</remarks>
internal enum MatchedField
{
    /// <summary>
    /// The name.
    /// </summary>
    Name = 0,

    /// <summary>
    /// An email.
    /// </summary>
    Email = 1,

    /// <summary>
    /// A phone.
    /// </summary>
    Phone = 2,
}
