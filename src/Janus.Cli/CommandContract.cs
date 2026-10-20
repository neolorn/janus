using System.Collections.Generic;
using Janus.Core;

namespace Janus.Cli;

/// <summary>
/// What a command declares beside its parser: its name, the arguments it takes, the
/// members of the document it reads on standard input, what it writes on standard output
/// when it exits 0, and the codes a refusal carries when it exits 1.
/// </summary>
/// <param name="Name">The command's name on the command line.</param>
/// <param name="Arguments">Each argument, in the order the command documents them.</param>
/// <param name="Input">The members of the JSON document on standard input, or none where it reads none.</param>
/// <param name="Output">Each line it writes on standard output on success, in the order it writes them.</param>
/// <param name="Refusals">The codes a refusal carries, each written as one JSON line on standard error.</param>
/// <remarks>
/// Implements LIB-API-001, LIB-TEST-002 and REF-001, as D-206 settles them. The contract
/// test renders every command's declaration into the committed contract list, so a
/// command, an argument, a member, a line or a code gained, lost or changed is seen
/// before a release. Every command exits 0 on success and 1 on a refusal (OPS-SEC-001).
/// </remarks>
internal sealed record CommandContract(
    string Name,
    IReadOnlyList<CommandArgument> Arguments,
    IReadOnlyList<string> Input,
    IReadOnlyList<CommandOutput> Output,
    IReadOnlyList<ErrorCode> Refusals);
