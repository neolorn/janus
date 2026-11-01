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
/// <c>--kind</c>, each destination a code is returned to as <c>--redirect</c>, what it
/// may ask for as <c>--scopes</c>, the scopes separated by spaces as a request carries
/// them, and the client its tokens are for as <c>--audience</c>.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-001, API-REDIR-001 and OPS-BOOT-001, as entry 340 of the
/// decisions pending review and D-252 settle them. The secret is never an argument: it
/// comes with the keys, so no process list shows it. <c>--redirect</c> is repeated for
/// each return address and <c>--audience</c> may be left out; that more than one
/// address and an audience are a public application's alone is the registry's to judge.
/// </remarks>
internal static class RegisterClientArguments
{
    private const string Prefix = "--";

    private const string Client = "client";

    private const string Name = "name";

    private const string Kind = "kind";

    private const string Redirect = "redirect";

    private const string Scopes = "scopes";

    private const string Audience = "audience";

    private static readonly string[] Members = [Client, Name, Kind, Redirect, Scopes, Audience];

    private static readonly string[] Required = [Client, Name, Kind, Redirect, Scopes];

    /// <summary>
    /// The arguments the command takes (LIB-API-001), each required but the audience.
    /// </summary>
    public static IReadOnlyList<CommandArgument> Declared { get; } =
    [
        new(Prefix + Client, Required: true, "text"),
        new(Prefix + Name, Required: true, "text"),
        new(
            Prefix + Kind,
            Required: true,
            "choice allowed=" + string.Join(',', Enum.GetValues<OidcClientKind>().Select(kind => WrittenName.Of(kind)).Order(StringComparer.Ordinal))),
        new(Prefix + Redirect, Required: true, "url repeatable"),
        new(Prefix + Scopes, Required: true, "text-list separator=space"),
        new(Prefix + Audience, Required: false, "text"),
    ];

    /// <summary>
    /// Reads the arguments that follow the command's name.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>
    /// The client they describe, or the failure naming the argument that is missing,
    /// unknown or repeated where it is not <c>--redirect</c>, or the kind that is not
    /// one of the three.
    /// </returns>
    /// <exception cref="ArgumentNullException">The arguments are absent.</exception>
    public static Result<OidcClient> Read(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var given = new Dictionary<string, string>(StringComparer.Ordinal);
        var redirects = new List<string>();

        for (int at = 0; at < arguments.Count; at += 2)
        {
            string option = arguments[at];

            if (!option.StartsWith(Prefix, StringComparison.Ordinal)
                || at + 1 == arguments.Count
                || !Members.Contains(option[Prefix.Length..], StringComparer.Ordinal)
                || (option[Prefix.Length..] is not Redirect && !given.TryAdd(option[Prefix.Length..], arguments[at + 1])))
            {
                return Result.Failure<OidcClient>(Malformed(option));
            }

            if (option[Prefix.Length..] is Redirect)
            {
                redirects.Add(arguments[at + 1]);
                given[Redirect] = redirects[0];
            }
        }

        foreach (string member in Required)
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
            redirects[0],
            given[Scopes].Split(' '))
        {
            OtherRedirects = [.. redirects.Skip(1)],
            Audience = given.GetValueOrDefault(Audience),
        });
    }

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));
}
