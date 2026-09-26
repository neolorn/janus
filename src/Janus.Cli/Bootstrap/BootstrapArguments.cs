using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Janus.Authentication.Bootstrap;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Cli.Bootstrap;

/// <summary>
/// Reads what the <c>bootstrap</c> command is given: the organization's name, the first
/// administrator's email and phone, optionally the address of their mailbox, and every
/// value the deployment names, each as <c>--name value</c>.
/// </summary>
/// <remarks>
/// Implements OPS-BOOT-001, LIB-HOST-001 and chapter 10 section 4. Every value is read
/// as its key admits it and the set is checked complete before the database is
/// reached, by the same rule the host's startup applies, so a deployment bootstrap
/// accepts is one that starts.
/// </remarks>
internal static class BootstrapArguments
{
    private const string Prefix = "--";

    private const string Organization = "organization";

    private const string Email = "email";

    private const string Phone = "phone";

    private const string Mailbox = "mailbox";

    /// <summary>
    /// Reads the arguments that follow the command's name.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>
    /// What they ask for, or the failure naming the argument that is missing, unknown,
    /// repeated or not admitted, or the key the deployment left unnamed.
    /// </returns>
    /// <exception cref="ArgumentNullException">The arguments are absent.</exception>
    public static Result<BootstrapRequest> Read(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var given = new Dictionary<string, string>(StringComparer.Ordinal);

        for (int at = 0; at < arguments.Count; at += 2)
        {
            string option = arguments[at];

            if (!option.StartsWith(Prefix, StringComparison.Ordinal)
                || at + 1 == arguments.Count
                || !given.TryAdd(option[Prefix.Length..], arguments[at + 1]))
            {
                return Result.Failure<BootstrapRequest>(Malformed(option));
            }
        }

        foreach (string member in (string[])[Organization, Email, Phone])
        {
            if (!given.ContainsKey(member))
            {
                return Result.Failure<BootstrapRequest>(Malformed(member));
            }
        }

        var named = new Dictionary<ConfigurationKey, string>();

        foreach ((string name, string value) in given)
        {
            if (name is Organization or Email or Phone or Mailbox)
            {
                continue;
            }

            // Only the values that name the deployment are bootstrap's to take; every
            // other key has its safe default until the management application changes it.
            if (Settings.Required.FirstOrDefault(setting => string.Equals(setting.Key.ToString(), name, StringComparison.Ordinal))
                is not Setting setting)
            {
                return Result.Failure<BootstrapRequest>(Malformed(Prefix + name));
            }

            Error? refused = null;

            setting.Apply(new WrittenForm(value)).Switch(written => named[setting.Key] = written, failure => refused = failure);

            if (refused is not null)
            {
                return Result.Failure<BootstrapRequest>(refused);
            }
        }

        if (Complete(named).Match(() => (Error?)null, failure => failure) is Error unnamed)
        {
            return Result.Failure<BootstrapRequest>(unnamed);
        }

        return Result.Success(new BootstrapRequest(
            given[Organization],
            given[Email],
            given[Phone],
            given.GetValueOrDefault(Mailbox),
            named));
    }

    // OPS-BOOT-001 AC4 and LIB-HOST-001: the rule the host's startup applies, over the
    // values named here and the defaults of the keys bootstrap does not take. The
    // records of processing are served by every deployment (entry 273).
    private static Result Complete(Dictionary<ConfigurationKey, string> named)
    {
        HostingLocation? location = named.TryGetValue(Settings.HostingLocation.Key, out string? written)
            ? Settings.HostingLocation.Read(written).Match(value => (HostingLocation?)value, _ => null)
            : null;

        try
        {
            Settings.ThrowIfIncomplete(
                named.Keys.ToHashSet(),
                location,
                Settings.PasswordBlocklistSource.Default,
                Settings.PasswordBlocklistSources.Default,
                recordsOfProcessing: true);
        }
        catch (StartupException incomplete)
        {
            return Result.Failure(incomplete.Failure ?? Error.From(ErrorCodes.StartupDeclarationMissing));
        }

        return Result.Success();
    }

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));
}
