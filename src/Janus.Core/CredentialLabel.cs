using System;

namespace Janus.Core;

/// <summary>
/// What a person calls one of their enrolled credentials, so that a list of three
/// passkeys says which is which.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-001 and CONV-DESIGN-004. One to sixty-four characters, unique
/// per kind per
/// account, defaulting to the client's description of the device
/// (<see cref="DeviceDescription"/>) and editable afterwards. The profile is
/// Nickname (RFC 8266), as a display name's is: a label is shown back as the person
/// wrote it, with the spaces and the normalization form settled. A default instance
/// was never read, so it has no label to give and no row can carry it.
/// </remarks>
public readonly record struct CredentialLabel
{
    /// <summary>
    /// The fewest characters a label carries.
    /// </summary>
    public const int MinimumLength = 1;

    /// <summary>
    /// The most characters a label carries.
    /// </summary>
    public const int MaximumLength = 64;

    private readonly string? _value;

    private CredentialLabel(string value) => _value = value;

    /// <summary>
    /// The label in the profile's form, which is what is shown.
    /// </summary>
    /// <exception cref="InvalidOperationException">The label was never set.</exception>
    public string Value => _value ?? throw new InvalidOperationException("The label was never set.");

    /// <summary>
    /// Reads a label as it was entered and returns it in the profile's form.
    /// </summary>
    /// <param name="entered">The label as it was entered.</param>
    /// <param name="label">The label, or an unset value.</param>
    /// <returns>Whether the value is a label this library accepts.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static bool TryParse(string entered, out CredentialLabel label)
    {
        ArgumentNullException.ThrowIfNull(entered);

        label = default;

        if (!Precis.TryEnforceNickname(entered, out string enforced)
            || enforced.Length is < MinimumLength or > MaximumLength)
        {
            return false;
        }

        label = new CredentialLabel(enforced);

        return true;
    }

    /// <summary>
    /// The label a credential takes where the person supplies none.
    /// </summary>
    /// <param name="device">The client's description of the device.</param>
    /// <returns>The label.</returns>
    /// <exception cref="ArgumentNullException">The description is absent.</exception>
    /// <exception cref="ArgumentException">
    /// The description does not read as a label, which a description assembled from
    /// two accepted parts does not.
    /// </exception>
    public static CredentialLabel Of(DeviceDescription device)
    {
        ArgumentNullException.ThrowIfNull(device);

        return TryParse(device.ToString(), out CredentialLabel label)
            ? label
            : throw new ArgumentException(
                "The device description does not read as a credential label.",
                nameof(device));
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The label was never set.</exception>
    public override string ToString() => Value;
}
