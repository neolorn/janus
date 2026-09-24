using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Cli.Tests;

/// <summary>
/// What the <c>bootstrap</c> command refuses before it reaches the database: the values
/// the deployment has to name, and the keys it reads from standard input (OPS-BOOT-001,
/// OPS-SEC-001). The connection each case names leads nowhere, so a case that reached
/// the database would fail rather than pass.
/// </summary>
public sealed class BootstrapRefusalTests
{
    private const string Nowhere = "Host=nowhere.invalid;Database=identity";

    /// <summary>
    /// OPS-BOOT-001 AC4: bootstrap without the governing language is refused, with the
    /// error that names it, and prints no link.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_AC4_BootstrapWithoutTheGoverningLanguageIsRefusedByNameAsync()
    {
        Invocation run = await Invocation.PipedAsync(
            Invocation.Bootstrap(Settings.LegalGoverningLanguage.Key),
            Invocation.Keys(Nowhere));

        Assert.Equal(1, run.ExitCode);
        Assert.Empty(run.Output);
        Assert.Equal(ErrorCodes.StartupGoverningLanguage.ToString(), Code(run));
    }

    /// <summary>
    /// OPS-BOOT-001 and LIB-HOST-001 AC2: each value that names the deployment is asked
    /// for by its key, so what bootstrap accepts is a deployment that starts.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_AValueTheDeploymentLeftUnnamedIsRefusedByItsKeyAsync()
    {
        Invocation run = await Invocation.PipedAsync(
            Invocation.Bootstrap(Settings.AlertingOwnerSms.Key),
            Invocation.Keys(Nowhere));

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(ErrorCodes.StartupDeclarationMissing.ToString(), Code(run));
        Assert.Equal(Settings.AlertingOwnerSms.Key.ToString(), Detail(run, "key"));
    }

    /// <summary>
    /// OPS-BOOT-001 and chapter 10 section 4: a value its key does not admit is refused
    /// with the code the key gives it, before anything is written.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_AValueItsKeyDoesNotAdmitIsRefusedAsync()
    {
        List<string> arguments = [.. Invocation.Bootstrap(Settings.HostingLocation.Key), "--" + Settings.HostingLocation.Key, "elsewhere"];

        Invocation run = await Invocation.PipedAsync(arguments, Invocation.Keys(Nowhere));

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(ErrorCodes.ConfigurationValueNotAllowed.ToString(), Code(run));
    }

    /// <summary>
    /// OPS-BOOT-001: bootstrap takes the values that name the deployment and nothing
    /// else; every other key keeps its safe default until the application changes it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_AKeyThatDoesNotNameTheDeploymentIsRefusedAsync()
    {
        List<string> arguments = [.. Invocation.Bootstrap(), "--" + Settings.SessionStepUpRecency.Key, "PT1M"];

        Invocation run = await Invocation.PipedAsync(arguments, Invocation.Keys(Nowhere));

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), Code(run));
        Assert.Equal("--" + Settings.SessionStepUpRecency.Key, Detail(run, "member"));
    }

    /// <summary>
    /// OPS-SEC-001 and INF-HOST-003: the keys are piped from the secrets manager, never
    /// typed, so a command whose standard input is a terminal is refused before it
    /// reads anything.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_001_TheCommandRefusesATerminalAsync()
    {
        Invocation run = await Invocation.TypedAsync(Invocation.Bootstrap());

        Assert.Equal(1, run.ExitCode);
        Assert.Empty(run.Output);
        Assert.Equal(ErrorCodes.StartupKeyUnavailable.ToString(), Code(run));
        Assert.Equal("input", Detail(run, "member"));
    }

    /// <summary>
    /// OPS-SEC-001 AC2: a document that does not carry a usable key-encryption key or
    /// fingerprint key is refused with the error that names which.
    /// </summary>
    /// <param name="member">The member the case leaves out.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("keyEncryptionKeys")]
    [InlineData("fingerprintKey")]
    public async Task OPS_SEC_001_AC2_TheCommandRefusesADocumentWithoutTheKeysAsync(string member)
    {
        JsonObject document = Invocation.Keys(Nowhere);
        Assert.True(document.Remove(member));

        Invocation run = await Invocation.PipedAsync(Invocation.Bootstrap(), document);

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(ErrorCodes.StartupKeyUnavailable.ToString(), Code(run));
        Assert.Equal(member, Detail(run, "member"));
    }

    /// <summary>
    /// OPS-SEC-001 AC2: a key-encryption key that is not an AES key, or whose current
    /// version is not among those given, is no key at all.
    /// </summary>
    /// <param name="current">The version the document calls current.</param>
    /// <param name="length">The length of the one version it gives.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(2, 32)]
    [InlineData(1, 20)]
    public async Task OPS_SEC_001_AC2_TheCommandRefusesAKeyThatCannotBeUsedAsync(int current, int length)
    {
        JsonObject document = Invocation.Keys(Nowhere);
        document["keyEncryptionKeys"] = new JsonObject
        {
            ["current"] = current,
            ["versions"] = new JsonObject { ["1"] = System.Convert.ToBase64String(new byte[length]) },
        };

        Invocation run = await Invocation.PipedAsync(Invocation.Bootstrap(), document);

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(ErrorCodes.StartupKeyUnavailable.ToString(), Code(run));
    }

    /// <summary>
    /// API-CONV-002 and CONV-CONTENT-001: a refusal names the member it concerns and
    /// nothing piped in: no key, and not the connection with its credential.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_001_ARefusalCarriesNoneOfTheDocumentAsync()
    {
        JsonObject document = Invocation.Keys(Nowhere + ";Password=the database credential");
        Assert.True(document.Remove("fingerprintKey"));

        Invocation run = await Invocation.PipedAsync(Invocation.Bootstrap(), document);

        Assert.Equal(1, run.ExitCode);
        Assert.DoesNotContain("the database credential", run.Error, System.StringComparison.Ordinal);
        Assert.DoesNotContain(Invocation.KeyEncryptionKey, run.Error, System.StringComparison.Ordinal);
    }

    private static string? Code(Invocation run)
    {
        using var refusal = JsonDocument.Parse(run.Error);

        return refusal.RootElement.GetProperty("code").GetString();
    }

    private static string? Detail(Invocation run, string name)
    {
        using var refusal = JsonDocument.Parse(run.Error);

        return refusal.RootElement.GetProperty("details").GetProperty(name).GetString();
    }
}
