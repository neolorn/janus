using System.Collections.Generic;

namespace Janus.Cli;

/// <summary>
/// One line, or a run of lines, a command writes on standard output on success.
/// </summary>
/// <param name="Form">
/// The media type of what is written: <c>application/json</c> or <c>text/plain</c> for
/// one line, <c>application/jsonl</c> for one JSON object a line, any number of them.
/// </param>
/// <param name="Members">The members of what is written, each with the type it is written as.</param>
/// <param name="Argument">
/// The argument that asks for it in place of what the command writes without that
/// argument, or nothing for what it writes where no such argument is given.
/// </param>
internal sealed record CommandOutput(string Form, IReadOnlyList<string> Members, string? Argument = null);
