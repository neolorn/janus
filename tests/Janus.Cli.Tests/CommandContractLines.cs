using System;
using System.Collections.Generic;
using System.Linq;

namespace Janus.Cli.Tests;

/// <summary>
/// The commands of the command-line application as a contract: what each declares
/// beside its parser, written as the lines of the committed contract file in the line
/// form of the endpoints' list (LIB-API-001, LIB-TEST-002, D-206).
/// </summary>
internal static class CommandContractLines
{
    private const string Line = "  ";

    private const string Member = "    ";

    private const string Succeeded = "0";

    private const string Refused = "1";

    /// <summary>
    /// The lines of the contract: each command by its name, then indented its arguments,
    /// whether each is required and the form of its value; the members of the document
    /// it reads on standard input; what it writes on standard output when it exits 0,
    /// each line by its media type, with the argument that asks for it, and its members;
    /// and the codes a refusal carries when it exits 1.
    /// </summary>
    /// <param name="contracts">What every command declares.</param>
    /// <returns>The lines.</returns>
    /// <exception cref="ArgumentNullException">The contracts are absent.</exception>
    public static IReadOnlyList<string> Of(IReadOnlyList<CommandContract> contracts)
    {
        ArgumentNullException.ThrowIfNull(contracts);

        var lines = new List<string>();

        foreach (CommandContract contract in contracts.OrderBy(contract => contract.Name, StringComparer.Ordinal))
        {
            lines.Add(contract.Name);

            if (contract.Arguments.Count is not 0)
            {
                lines.Add(Line + "arguments");
                lines.AddRange(contract.Arguments.Select(argument =>
                    Member + argument.Name + " " + (argument.Required ? "required" : "optional") + " " + argument.Form));
            }

            if (contract.Input.Count is not 0)
            {
                lines.Add(Line + "input application/json");
                lines.AddRange(contract.Input.Select(member => Member + member));
            }

            lines.Add(Line + Succeeded);

            foreach (CommandOutput output in contract.Output)
            {
                lines.Add(Member + output.Form + (output.Argument is string argument ? " " + argument : string.Empty));
                lines.AddRange(output.Members.Select(member => Member + Line + member));
            }

            lines.Add(Line
                + Refused
                + " "
                + string.Join(' ', contract.Refusals.Select(code => code.ToString()).Order(StringComparer.Ordinal)));
        }

        return lines;
    }
}
