namespace HostSample;

/// <summary>
/// One value a letter carries, which the courier puts in its place in the workshop's
/// wording of the message.
/// </summary>
internal sealed class LetterPlace
{
    /// <summary>
    /// The letter, under its reference.
    /// </summary>
    public required string Reference { get; init; }

    /// <summary>
    /// The place's name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// What goes in it.
    /// </summary>
    public required string Value { get; init; }
}
