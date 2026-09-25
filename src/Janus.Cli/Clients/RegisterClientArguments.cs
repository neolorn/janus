using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Janus.Authentication;
using Janus.Core;

namespace Janus.Cli.Clients;

/// <summary>
/// Reads what the <c>register-client</c> command is given: the client's identifier as
/// <c>--client</c>, what the deployment calls it as <c>--name</c>, its kind as
/// <c>--kind</c>, the one destination a code is returned to as <c>--redirect</c> and
/// what it may ask for as <c>--scopes</c>, the scopes separated by spaces as a request
/// carries them.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-001 and API-REDIR-001, as entry 340 of the decisions pending
/// review settles them. The secret is never an argument: it comes with the keys, so no
/// process list shows it.
/// </remarks>
internal static class RegisterClientArguments
{
    private const string Prefix = "--";

    private const string Client = "client";

    private const string Name = "name";

    private const string Kind = "kind";

    private const string Redirect = "redirect";

    private const string Scopes = "scopes";

    private static readonly string[] Members = [Client, Name, Kind, Redirect, Scopes];

    /// <summary>
    /// Reads the arguments that follow the command's name.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>
    /// The client they describe, or the failure naming the argument that is missing,
    /// unknown or repeated, or the kind that is not one of the two.
    /// </returns>
    /// <exception cref="ArgumentNullException">The arguments are absent.</exception>
    public static Result<OidcClient> Read(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var given = new Dictionary<string, string>(StringComparer.Ordinal);

        for (int at = 0; at < arguments.Count; at += 2)
        {
            string option = arguments[at];

            if (!option.StartsWith(Prefix, StringComparison.Ordinal)
                || at + 1 == arguments.Count
                || !Members.Contains(option[Prefix.Length..], StringComparer.Ordinal)
                || !given.TryAdd(option[Prefix.Length..], arguments[at + 1]))
            {
                return Result.Failure<OidcClient>(Malformed(option));
            }
        }

        foreach (string member in Members)
        {
            if (!given.ContainsKey(member))
            {
                return Result.Failure<OidcClient>(Malformed(member));
            }
        }

        if (Enum.GetValues<OidcClientKind>()
                .Where(kind => string.Equals(WrittenName.Of(kind), given[Kind], StringComparison.Ordinal))
                .Select(kind => (OidcClientKind?)kind)
                .FirstOrDefault()
            is not OidcClientKind named)
        {
            return Result.Failure<OidcClient>(Malformed(Kind));
        }

        return Result.Success(new OidcClient(
            given[Client],
            given[Name],
            named,
            given[Redirect],
            given[Scopes].Split(' ')));
    }

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));
}
