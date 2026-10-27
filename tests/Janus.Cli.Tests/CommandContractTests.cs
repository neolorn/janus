using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Xunit;

namespace Janus.Cli.Tests;

/// <summary>
/// The commands of the command-line application as a contract: the name, arguments,
/// standard input, standard output and exit codes each declares beside its parser, held
/// to the committed contract file (LIB-API-001, LIB-TEST-002, D-206).
/// </summary>
[Trait("kind", "contract")]
public sealed class CommandContractTests
{
    private const string BrowserApplication = "browser-application";

    /// <summary>
    /// LIB-API-001 AC2: what every command declares is the committed contract file, so a
    /// command added or dropped, an argument gained, lost or made required, a member of
    /// its document, a line it writes or a code its refusal carries each fail here and
    /// carry their version bump.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_TheCommandsAreTheContract() =>
        Assert.Equal(
            Repository.ReadText("tests/Janus.Cli.Tests/commands.txt")
                .ReplaceLineEndings("\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries),
            CommandContractLines.Of(Program.Contracts));

    /// <summary>
    /// LIB-API-001 AC2: every command the executable carries declares its contract under
    /// the name it is run by, each name once, and a refusal it gives carries a code its
    /// contract declares.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_API_001_AC2_EveryCommandTheExecutableCarriesDeclaresItsContractAsync()
    {
        Assert.Equal(
            Program.Contracts.Select(contract => contract.Name).Distinct(StringComparer.Ordinal),
            Program.Contracts.Select(contract => contract.Name));

        foreach (CommandContract contract in Program.Contracts)
        {
            Invocation run = await Invocation.TypedAsync([contract.Name]);
            string code = Reported.Refusal(run);

            Assert.Contains(code, contract.Refusals.Select(declared => declared.ToString()));
            Assert.NotEqual(contract.Name, Detail(run, "member"));
        }
    }

    /// <summary>
    /// LIB-API-001 AC2: the arguments <c>bootstrap</c> declares required are those it
    /// refuses to read without, and those it declares optional it reads without, so the
    /// contract states what the parser takes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_API_001_AC2_BootstrapRefusesToReadWithoutWhatItDeclaresRequiredAsync()
    {
        IReadOnlyList<string> named = Invocation.Bootstrap();

        await AssertDeclaredAsync(Contract("bootstrap"), named);
    }

    /// <summary>
    /// LIB-API-001 AC2: the arguments <c>register-client</c> declares required are those
    /// it refuses to read without.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_API_001_AC2_RegisterClientRefusesToReadWithoutWhatItDeclaresRequiredAsync()
    {
        IReadOnlyList<string> named =
        [
            "register-client",
            "--client", "storefront",
            "--name", "storefront",
            "--kind", BrowserApplication,
            "--redirect", "https://shop.example.test/auth/signon/return",
            "--scopes", "openid",
        ];

        await AssertDeclaredAsync(Contract("register-client"), named);
    }

    // Every argument is read, so the run reaches the document and is refused for the
    // terminal it was started at; each required argument left out is refused before
    // that, and each optional one named left out still reaches it.
    private static async Task AssertDeclaredAsync(CommandContract contract, IReadOnlyList<string> named)
    {
        string unread = ErrorCodes.StartupSecretUnavailable.ToString();

        Assert.Equal(unread, Reported.Refusal(await Invocation.TypedAsync(named)));

        foreach (CommandArgument argument in contract.Arguments.Where(argument => named.Contains(argument.Name)))
        {
            string code = Reported.Refusal(await Invocation.TypedAsync(Without(named, argument.Name)));

            if (argument.Required)
            {
                Assert.NotEqual(unread, code);
            }
            else
            {
                Assert.Equal(unread, code);
            }
        }

        Assert.All(
            contract.Arguments.Where(argument => argument.Required),
            argument => Assert.Contains(argument.Name, named));
    }

    private static CommandContract Contract(string name) =>
        Program.Contracts.Single(contract => string.Equals(contract.Name, name, StringComparison.Ordinal));

    // The arguments less one option and the value after it.
    private static List<string> Without(IReadOnlyList<string> arguments, string option)
    {
        int at = arguments.ToList().IndexOf(option);

        return [.. arguments.Take(at), .. arguments.Skip(at + 2)];
    }

    private static string? Detail(Invocation run, string name)
    {
        using var refusal = JsonDocument.Parse(run.Error);

        return refusal.RootElement.GetProperty("details").TryGetProperty(name, out JsonElement value)
            ? value.GetString()
            : null;
    }
}
