using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Cli.Tests;

/// <summary>
/// One run of the command-line application, as a person at the server would start it:
/// the arguments, the key document piped to standard input, and what came back.
/// </summary>
/// <param name="ExitCode">The exit code.</param>
/// <param name="Output">What was written to standard output.</param>
/// <param name="Error">What was written to standard error.</param>
public sealed record Invocation(int ExitCode, string Output, string Error)
{
    /// <summary>
    /// The administrative organization's name every bootstrap here names.
    /// </summary>
    public const string Organization = "Administration";

    /// <summary>
    /// The first administrator's personal email.
    /// </summary>
    public const string Email = "admin@example.test";

    /// <summary>
    /// The first administrator's phone number.
    /// </summary>
    public const string Phone = "+201000000003";

    /// <summary>
    /// The corporate address the administrator's mailbox is queued at.
    /// </summary>
    public const string Mailbox = "admin@corp.example.test";

    /// <summary>
    /// The origin the authentication application answers at.
    /// </summary>
    public const string Origin = "https://accounts.example.test";

    /// <summary>
    /// The deployment's key-encryption key in base64, drawn once for the run of the suite.
    /// </summary>
    public static string KeyEncryptionKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// The deployment's fingerprint key in base64, drawn once for the run of the suite.
    /// </summary>
    public static string FingerprintKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Every value a deployment names, by its key, as the host fixtures name them.
    /// </summary>
    /// <returns>The written form of each value, by its key.</returns>
    public static IReadOnlyDictionary<ConfigurationKey, string> Named() =>
        new Dictionary<ConfigurationKey, string>
        {
            [Settings.WebAuthnOrigins.Key] = Settings.WebAuthnOrigins.Write([Origin]),
            [Settings.HostingLocation.Key] = Settings.HostingLocation.Write(HostingLocation.Inside),
            [Settings.HostingEnvironment.Key] = Settings.HostingEnvironment.Write("a rented virtual machine"),
            [Settings.AlertingEmailDestinations.Key] = Settings.AlertingEmailDestinations.Write(["operator@example.test"]),
            [Settings.AlertingSmsDestinations.Key] = Settings.AlertingSmsDestinations.Write(["+201000000001"]),
            [Settings.AlertingOwnerEmail.Key] = Settings.AlertingOwnerEmail.Write("owner@example.test"),
            [Settings.AlertingOwnerSms.Key] = Settings.AlertingOwnerSms.Write("+201000000002"),
            [Settings.AbuseSmsBalanceFloor.Key] = Settings.AbuseSmsBalanceFloor.Write(100m),
            [Settings.LegalGoverningLanguage.Key] = Settings.LegalGoverningLanguage.Write("ar"),
            [Settings.PrivacyCalendarTimeZone.Key] = Settings.PrivacyCalendarTimeZone.Write("Africa/Cairo"),
            [Settings.NotificationLanguages.Key] = Settings.NotificationLanguages.Write(["en"]),
            [Settings.NotificationEmailSendingDomain.Key] = Settings.NotificationEmailSendingDomain.Write("mail.example.test"),
        };

    /// <summary>
    /// The arguments of a bootstrap that names everything, less what a case leaves out.
    /// </summary>
    /// <param name="without">The keys the case does not name.</param>
    /// <returns>The arguments, the command's name first.</returns>
    public static IReadOnlyList<string> Bootstrap(params ConfigurationKey[] without)
    {
        List<string> arguments =
        [
            "bootstrap",
            "--organization", Organization,
            "--email", Email,
            "--phone", Phone,
            "--mailbox", Mailbox,
        ];

        foreach ((ConfigurationKey key, string value) in Named())
        {
            if (!((IList<ConfigurationKey>)without).Contains(key))
            {
                arguments.AddRange(["--" + key, value]);
            }
        }

        return arguments;
    }

    /// <summary>
    /// The document the operator pipes from the secrets manager.
    /// </summary>
    /// <param name="connection">The connection the command runs under.</param>
    /// <returns>The document, whole.</returns>
    public static JsonObject Keys(string connection) =>
        new()
        {
            ["connection"] = connection,
            ["keyEncryptionKeys"] = new JsonObject
            {
                ["current"] = 1,
                ["versions"] = new JsonObject { ["1"] = KeyEncryptionKey },
            },
            ["fingerprintKey"] = FingerprintKey,
        };

    /// <summary>
    /// Runs the application with a document piped to standard input.
    /// </summary>
    /// <param name="arguments">The arguments, the command's name first.</param>
    /// <param name="document">What is piped in.</param>
    /// <returns>What came back.</returns>
    public static Task<Invocation> PipedAsync(IReadOnlyList<string> arguments, JsonNode document) =>
        RunAsync(arguments, Encoding.UTF8.GetBytes(document.ToJsonString()), redirected: true);

    /// <summary>
    /// Runs the application from a terminal, with nothing piped in.
    /// </summary>
    /// <param name="arguments">The arguments, the command's name first.</param>
    /// <returns>What came back.</returns>
    public static Task<Invocation> TypedAsync(IReadOnlyList<string> arguments) =>
        RunAsync(arguments, [], redirected: false);

    private static async Task<Invocation> RunAsync(IReadOnlyList<string> arguments, byte[] input, bool redirected)
    {
        await using var piped = new MemoryStream(input);
        await using var output = new StringWriter();
        await using var error = new StringWriter();

        int exitCode = await Program.RunAsync(
            arguments,
            new Terminal(piped, redirected, output, error),
            TestContext.Current.CancellationToken);

        return new Invocation(exitCode, output.ToString(), error.ToString());
    }
}
