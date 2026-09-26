using System.IO;

namespace Janus.Cli;

/// <summary>
/// What a command reads from and writes to: the process's standard streams, or the
/// ones a test hands it.
/// </summary>
/// <param name="Input">Standard input, read as bytes so no key passes through a string.</param>
/// <param name="IsInputRedirected">Whether standard input is piped rather than typed.</param>
/// <param name="Output">Standard output, which carries what a command produces.</param>
/// <param name="Error">Standard error, which carries why a command refused.</param>
internal sealed record Terminal(Stream Input, bool IsInputRedirected, TextWriter Output, TextWriter Error);
