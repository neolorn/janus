using System;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Cli.Migration;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Cli.Tests;

/// <summary>
/// What <c>migrate</c> reads: the connection alone, piped in, and no key (OPS-MIG-001,
/// OPS-SEC-001, CONV-DESIGN-007, D-204).
/// </summary>
[Trait("kind", "unit")]
public sealed class MigrateDocumentTests
{
    private const string Migrate = "migrate";

    private const string Nowhere = "Host=nowhere.invalid;Database=identity";

    /// <summary>
    /// OPS-SEC-001 AC4: <c>migrate</c> started at a terminal is refused with
    /// <c>model.startup.secretunavailable</c> naming the input, before it reads anything.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_001_AC4_MigrateRefusesATerminalAsync()
    {
        Invocation run = await Invocation.TypedAsync([Migrate]);

        Assert.Equal(ErrorCodes.StartupSecretUnavailable.ToString(), Reported.Refusal(run));
        Assert.Equal("input", Detail(run, "key"));
    }

    /// <summary>
    /// OPS-SEC-001 AC4, OPS-MIG-001: a document that carries no connection member is
    /// refused with <c>model.startup.secretunavailable</c> naming the input, and so is
    /// one that is no object at all.
    /// </summary>
    /// <param name="document">What is piped in.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("{}")]
    [InlineData("""["Host=nowhere.invalid"]""")]
    public async Task OPS_SEC_001_AC4_MigrateRefusesADocumentCarryingNoConnectionAsync(string document)
    {
        Invocation run = await Invocation.WrittenAsync([Migrate], document);

        Assert.Equal(ErrorCodes.StartupSecretUnavailable.ToString(), Reported.Refusal(run));
        Assert.Equal("input", Detail(run, "key"));
    }

    /// <summary>
    /// OPS-MIG-001 AC4, OPS-SEC-001 AC4: a connection member that is blank or not a
    /// string is refused with <c>api.request.malformed</c> naming the connection, as the
    /// other commands answer it (D-209).
    /// </summary>
    /// <param name="document">What is piped in.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("""{"connection": ""}""")]
    [InlineData("""{"connection": "  "}""")]
    [InlineData("""{"connection": 5432}""")]
    [InlineData("""{"connection": null}""")]
    [InlineData("""{"connection": {"host": "nowhere.invalid"}}""")]
    public async Task OPS_MIG_001_AC4_ABlankOrNonStringConnectionIsRefusedAsMalformedAsync(string document)
    {
        Invocation run = await Invocation.WrittenAsync([Migrate], document);

        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), Reported.Refusal(run));
        Assert.Equal("connection", Detail(run, "member"));
    }

    /// <summary>
    /// OPS-MIG-001 AC4: a document carrying any member but the connection is refused with
    /// <c>api.request.malformed</c> naming the input, with the connection or without it,
    /// and the refusal carries nothing of the document.
    /// </summary>
    /// <param name="document">What is piped in.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("""{"connection": "Host=nowhere.invalid;Password=the database credential", "keyEncryptionKeys": {"current": 1, "versions": {"1": "AAAA"}}}""")]
    [InlineData("""{"keyEncryptionKeys": {"current": 1, "versions": {"1": "AAAA"}}}""")]
    [InlineData("""{"connection": "Host=nowhere.invalid;Password=the database credential", "reason": "upgrade"}""")]
    [InlineData("""{"connection": "", "fingerprintKeys": {}}""")]
    public async Task OPS_MIG_001_AC4_ADocumentCarryingAnotherMemberIsRefusedAsMalformedAsync(string document)
    {
        Invocation run = await Invocation.WrittenAsync([Migrate], document);

        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), Reported.Refusal(run));
        Assert.Equal("input", Detail(run, "member"));
        Assert.DoesNotContain("the database credential", run.Error, StringComparison.Ordinal);
    }

    /// <summary>
    /// OPS-SEC-001 AC4: what is piped in and is not JSON, or is longer than 64 KiB, is
    /// refused as malformed naming the input.
    /// </summary>
    /// <param name="length">How long the document is made, or nothing for text that is not JSON.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData((64 * 1024) + 1)]
    public async Task OPS_SEC_001_AC4_MigrateRefusesADocumentThatDoesNotReadAsMalformedAsync(int? length)
    {
        string document = JsonSerializer.Serialize(new { connection = Nowhere });
        string piped = length is int padded
            ? document + new string(' ', padded - document.Length)
            : "connection=" + Nowhere;

        Invocation run = await Invocation.WrittenAsync([Migrate], piped);

        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), Reported.Refusal(run));
        Assert.Equal("input", Detail(run, "member"));
    }

    /// <summary>
    /// OPS-MIG-001: the command takes no argument, and one given is refused by name
    /// before anything is read.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_MIG_001_AnArgumentIsRefusedByNameAsync()
    {
        Invocation run = await Invocation.TypedAsync([Migrate, "--force"]);

        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), Reported.Refusal(run));
        Assert.Equal("--force", Detail(run, "member"));
    }

    /// <summary>
    /// OPS-MIG-001 AC5: the report takes <c>--report</c> once and nothing beside it, and
    /// an argument beside it is refused by name before anything is read.
    /// </summary>
    /// <param name="refused">The argument given beside <c>--report</c>, which is refused by name.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("--report")]
    [InlineData("--force")]
    public async Task OPS_MIG_001_AC5_AnArgumentBesideTheReportIsRefusedByNameAsync(string refused)
    {
        Invocation run = await Invocation.TypedAsync([Migrate, "--report", refused]);

        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), Reported.Refusal(run));
        Assert.Equal(refused, Detail(run, "member"));
    }

    /// <summary>
    /// OPS-MIG-001 AC5, OPS-SEC-001 AC4: the report reads its document as the run does,
    /// so it is refused at a terminal, and refused as malformed naming the input where
    /// the document carries a key, each a refusal that exits 1.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_MIG_001_AC5_TheReportIsRefusedAsTheRunIsAsync()
    {
        Invocation typed = await Invocation.TypedAsync([Migrate, "--report"]);
        Invocation keyed = await Invocation.PipedAsync([Migrate, "--report"], Invocation.Keys(Nowhere));

        Assert.Equal(ErrorCodes.StartupSecretUnavailable.ToString(), Reported.Refusal(typed));
        Assert.Equal("input", Detail(typed, "key"));
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), Reported.Refusal(keyed));
        Assert.Equal("input", Detail(keyed, "member"));
    }

    /// <summary>
    /// OPS-MIG-001 AC4, CONV-DESIGN-007: what the command runs over registers no key
    /// ring, so nothing of it can be filled.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_MIG_001_AC4_TheCommandRegistersNoKeyRingAsync()
    {
        await using ServiceProvider services = MigrateCommand.Composed(Nowhere);

        Assert.Null(services.GetService<IKeyRing>());
    }

    private static string? Detail(Invocation run, string name)
    {
        using var refusal = JsonDocument.Parse(run.Error);

        return refusal.RootElement.GetProperty("details").GetProperty(name).GetString();
    }
}
