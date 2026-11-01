using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Cli.Organizations;

/// <summary>
/// Reads what the <c>organization</c> command is given: the organization's name as
/// <c>--name</c>, the file holding its policy as <c>--policy</c>, the reason as
/// <c>--reason</c> and, optionally, whether an account may hold several memberships as
/// <c>--multiplememberships</c>.
/// </summary>
/// <remarks>
/// Implements OPS-BOOT-001 and D-248. Every argument is judged as <c>bootstrap</c>
/// judges its own: a name the command does not take, one given twice, without a value
/// or not beginning <c>--</c> is refused naming it, and a value its key does not admit
/// is refused with that key's code before the database is reached.
/// </remarks>
internal static class OrganizationArguments
{
    private const string Prefix = "--";

    private const string Name = "name";

    private const string Policy = "policy";

    private const string Reason = "reason";

    private const string MultipleMemberships = "multiplememberships";

    private static readonly string[] Members = [Name, Policy, Reason, MultipleMemberships];

    /// <summary>
    /// The arguments the command takes (LIB-API-001): the name, the policy file and the
    /// reason, each required, and whether an account may hold several memberships, in
    /// the written form its key admits.
    /// </summary>
    public static IReadOnlyList<CommandArgument> Declared { get; } =
    [
        new(Prefix + Name, Required: true, "text"),
        new(Prefix + Policy, Required: true, "path"),
        new(Prefix + Reason, Required: true, "text"),
        new(Prefix + MultipleMemberships, Required: false, "setting"),
    ];

    /// <summary>
    /// Reads the arguments that follow the command's name.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>
    /// What they ask for, or the failure naming the argument that is missing, unknown,
    /// repeated or without a value, the reason's absence, or the value its key does not
    /// admit.
    /// </returns>
    /// <exception cref="ArgumentNullException">The arguments are absent.</exception>
    public static Result<OrganizationRequest> Read(IReadOnlyList<string> arguments)
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
                return Result.Failure<OrganizationRequest>(Malformed(option));
            }
        }

        foreach (string member in (string[])[Name, Policy])
        {
            if (!given.ContainsKey(member))
            {
                return Result.Failure<OrganizationRequest>(Malformed(member));
            }
        }

        // OPS-CFG-005 and D-248: every change the command makes carries the reason, so a
        // command line without one is refused as a change without one is.
        if (!given.TryGetValue(Reason, out string? reason))
        {
            return Result.Failure<OrganizationRequest>(Error.From(ErrorCodes.ConfigurationChangeReasonRequired));
        }

        bool? multiple = null;

        if (given.TryGetValue(MultipleMemberships, out string? entered))
        {
            Error? refused = null;

            Settings.OrganizationMultipleMemberships
                .Entered(entered)
                .Switch(value => multiple = value, failure => refused = failure);

            if (refused is not null)
            {
                return Result.Failure<OrganizationRequest>(refused);
            }
        }

        return Result.Success(new OrganizationRequest(given[Name], given[Policy], reason, multiple));
    }

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));
}
