namespace Janus.Core;

/// <summary>
/// What a host-registered key supplier is told about a send, so it can answer the
/// value its restriction counts against.
/// </summary>
/// <param name="Purpose">What the send is for.</param>
/// <param name="Kind">The channel it goes out on.</param>
/// <param name="Subject">Whose account it belongs to, where it belongs to one.</param>
/// <param name="Source">The address it was asked for from.</param>
/// <remarks>Implements LIB-HOST-001, AUTH-ABUSE-004, D-153.</remarks>
public sealed record SendContext(
    RestrictionPurpose Purpose,
    SendKind Kind,
    SubjectId? Subject,
    string Source);
