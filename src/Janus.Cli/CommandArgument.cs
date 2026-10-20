namespace Janus.Cli;

/// <summary>
/// One argument a command takes.
/// </summary>
/// <param name="Name">The argument as it is written, an option with its <c>--</c> or a value in its place.</param>
/// <param name="Required">Whether the command refuses to run without it.</param>
/// <param name="Form">The form of its value: <c>flag</c> where it takes none.</param>
internal sealed record CommandArgument(string Name, bool Required, string Form);
