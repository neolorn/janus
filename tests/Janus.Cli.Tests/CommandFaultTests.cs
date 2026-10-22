using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Xunit;

namespace Janus.Cli.Tests;

/// <summary>
/// What every command answers a fault that is not a refusal: one JSON line on standard
/// error carrying <c>system.fault</c>, nothing on standard output, exit code 1, and its
/// keys cleared (OPS-SEC-001, D-207).
/// </summary>
[Trait("kind", "unit")]
public sealed class CommandFaultTests : IDisposable
{
    // A connection the provider cannot read, so the command's work throws a fault that
    // carries no code the first time it reaches the database.
    private const string Unreadable = "Host=nowhere.invalid;Port=notanumber";

    private readonly string _ledger = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    /// <inheritdoc/>
    public void Dispose() => File.Delete(_ledger);

    /// <summary>
    /// OPS-SEC-001 AC5: every command the executable carries, its arguments read and its
    /// document read, whose work throws a fault that carries no code, writes one JSON
    /// line <c>{"code": "system.fault", "details": {}}</c> on standard error and nothing on
    /// standard output, and exits 1.
    /// </summary>
    /// <param name="command">The command named.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("bootstrap")]
    [InlineData("rotate-kek")]
    [InlineData("rotate-fingerprint-key")]
    [InlineData("configure")]
    [InlineData("replay-erasures")]
    [InlineData("register-client")]
    [InlineData("migrate")]
    public async Task OPS_SEC_001_AC5_AFaultThatCarriesNoCodeEndsEveryCommandWithSystemFaultAsync(string command)
    {
        await File.WriteAllTextAsync(
            _ledger,
            $"2026-08-14T09:22:05Z {Guid.NewGuid():D} erasure-request\n",
            TestContext.Current.CancellationToken);

        JsonNode document = command is "migrate"
            ? new JsonObject { ["connection"] = Unreadable }
            : Invocation.Keys(Unreadable);

        Invocation run = await Invocation.PipedAsync(Arguments(command), document);

        Assert.Equal(ErrorCodes.SystemFault.ToString(), Reported.Refusal(run));
        Assert.Equal("""{"code":"system.fault","details":{}}""", run.Error.TrimEnd('\r', '\n'));
    }

    /// <summary>
    /// OPS-SEC-001 AC5: a fault thrown while a command holds the keys its document carried
    /// clears every one of them on its way out, so the command it ends leaves no key
    /// behind and a read of its ring throws.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_001_AC5_AFaultClearsEveryKeyTheCommandReadAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IKeyRing? ring = null;

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            using var held = new HeldKeys();
            await using var piped = new MemoryStream(Encoding.UTF8.GetBytes(Invocation.Keys(Unreadable).ToJsonString()));

            ring = (await KeyDocument.ReadAsync(
                    new Terminal(piped, IsInputRedirected: true, TextWriter.Null, TextWriter.Null),
                    held,
                    cancellationToken))
                .Match(document => document.Ring, error => throw new InvalidOperationException(error.Code.ToString()));

            throw new InvalidDataException();
        });

        Assert.NotNull(ring);
        Assert.Throws<InvalidOperationException>(() => ring.BorrowKeyEncryptionKeys(keys => keys.CurrentVersion));
        Assert.Throws<InvalidOperationException>(() => ring.BorrowFingerprintKeys(keys => keys.CurrentVersion));
    }

    // The arguments each command reads in full, so its run reaches its work.
    private IReadOnlyList<string> Arguments(string command) =>
        command switch
        {
            "bootstrap" => Invocation.Bootstrap(),
            "configure" => [command, "--abuse.throttle.enabled", "false", "--reason", "The throttle is under review."],
            "replay-erasures" => [command, _ledger],
            "register-client" =>
            [
                command,
                "--client", "storefront",
                "--name", "storefront",
                "--kind", "browser-application",
                "--redirect", "https://shop.example.test/auth/signon/return",
                "--scopes", "openid",
            ],
            _ => [command],
        };
}
