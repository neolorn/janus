namespace Janus.Core;

/// <summary>
/// One email address or phone number of another person's account.
/// </summary>
/// <param name="Value">The form the person entered.</param>
/// <param name="Verified">Whether it counts.</param>
/// <param name="Primary">Whether it is the primary of its kind.</param>
/// <remarks>Implements IDN-READ-003.</remarks>
public sealed record AccountViewIdentifier(string Value, bool Verified, bool Primary);
