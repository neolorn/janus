using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Janus.Authentication.Configuration;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Cli.Configuration;

/// <summary>
/// Reads what the <c>configure</c> command is given: each protected key it changes as
/// <c>--key value</c>, and the reason as <c>--reason text</c>.
/// </summary>
/// <remarks>
/// Implements OPS-CFG-004 and chapter 10 section 4.8. Only a protected key is taken: a
/// key the application may change is changed through the application, where its
/// direction prices the change (OPS-CFG-002), and never from the server.
/// </remarks>
internal static class ConfigureArguments
{
    private const string Prefix = "--";

    private const string Reason = "reason";

    /// <summary>
    /// Reads the arguments that follow the command's name.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>
    /// What they ask for, or the failure naming the argument that is missing, unknown,
    /// repeated or not a protected key, or the value its key does not admit.
    /// </returns>
    /// <exception cref="ArgumentNullException">The arguments are absent.</exception>
    public static Result<ConfigureRequest> Read(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var given = new Dictionary<string, string>(StringComparer.Ordinal);
        var order = new List<string>();

        for (int at = 0; at < arguments.Count; at += 2)
        {
            string option = arguments[at];

            if (!option.StartsWith(Prefix, StringComparison.Ordinal)
                || at + 1 == arguments.Count
                || !given.TryAdd(option[Prefix.Length..], arguments[at + 1]))
            {
                return Result.Failure<ConfigureRequest>(Malformed(option));
            }

            order.Add(option[Prefix.Length..]);
        }

        var values = new List<ProtectedValue>();

        foreach (string name in order.Where(name => name != Reason))
        {
            Error? refused = null;

            Read(name, given[name]).Switch(values.Add, failure => refused = failure);

            if (refused is not null)
            {
                return Result.Failure<ConfigureRequest>(refused);
            }
        }

        return values.Count is 0
            ? Result.Failure<ConfigureRequest>(Malformed("key"))
            : Result.Success(new ConfigureRequest(values, given.GetValueOrDefault(Reason)));
    }

    // A protected key that exists once, or one organization's member of a protected
    // family, named with the organization identifier as the key holds it.
    private static Result<ProtectedValue> Read(string name, string entered)
    {
        if (Settings.All.FirstOrDefault(setting => string.Equals(setting.Key.ToString(), name, StringComparison.Ordinal))
            is { Scope: SettingScope.Protected } setting)
        {
            return setting.Apply(new ProtectedReading(entered));
        }

        foreach (SettingFamily family in Settings.Families.Where(family => family.Scope is SettingScope.Protected))
        {
            string parameter = name.StartsWith(family.Prefix + ".", StringComparison.Ordinal)
                ? name[(family.Prefix.Length + 1)..]
                : string.Empty;

            if (Guid.TryParseExact(parameter, "D", out Guid identifier)
                && string.Equals(parameter, identifier.ToString("D", CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                return family.Apply(new ProtectedMemberReading(new OrganizationId(identifier), entered));
            }
        }

        return Result.Failure<ProtectedValue>(Malformed(Prefix + name));
    }

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));
}
