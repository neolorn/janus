using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Xunit;

namespace Janus.Cli.Tests;

/// <summary>
/// What the <c>organization</c> command refuses before the database is reached: its
/// arguments, judged as <c>bootstrap</c> judges its own, and the policy file, read as the
/// policy route reads its policy object (OPS-BOOT-001, D-248).
/// </summary>
[Trait("kind", "unit")]
public sealed class OrganizationArgumentTests : IDisposable
{
    private const string Reason = "Production is set up.";

    private readonly string _policy = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    /// <summary>
    /// A policy file the policy route would take.
    /// </summary>
    public OrganizationArgumentTests() =>
        File.WriteAllText(_policy, """{ "requiredAssurance": "aal2", "grammaticalGender": true }""");

    /// <inheritdoc/>
    public void Dispose() => File.Delete(_policy);

    /// <summary>
    /// OPS-BOOT-001 (D-248): with every argument read, the run reaches the key document,
    /// which a terminal does not give; so the arguments themselves were taken, the
    /// optional one with or without it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_TheOrganizationCommandReadsItsArgumentsAsync()
    {
        Invocation all = await Invocation.TypedAsync(
            ["organization", "--name", "Northwind Clinics", "--policy", _policy, "--reason", Reason, "--multiplememberships", "true"]);
        Invocation without = await Invocation.TypedAsync(
            ["organization", "--reason", Reason, "--policy", _policy, "--name", "Northwind Clinics"]);

        Assert.Equal(ErrorCodes.StartupSecretUnavailable.ToString(), Reported.Refusal(all));
        Assert.Equal(ErrorCodes.StartupSecretUnavailable.ToString(), Reported.Refusal(without));
    }

    /// <summary>
    /// OPS-BOOT-001 (D-248): an unknown name, one given twice, without a value or not
    /// beginning <c>--</c> is refused with <c>api.request.malformed</c> naming it, a
    /// required argument left out names that argument, and a
    /// <c>--multiplememberships</c> other than <c>true</c> or <c>false</c> is refused
    /// with its key's code.
    /// </summary>
    /// <param name="case">Which argument the case gets wrong.</param>
    /// <param name="code">The refusal's code.</param>
    /// <param name="member">The argument the refusal names, or nothing where it names none.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("unknown", "api.request.malformed", "--organization")]
    [InlineData("twice", "api.request.malformed", "--name")]
    [InlineData("valueless", "api.request.malformed", "--multiplememberships")]
    [InlineData("unprefixed", "api.request.malformed", "name")]
    [InlineData("nameless", "api.request.malformed", "name")]
    [InlineData("policyless", "api.request.malformed", "policy")]
    [InlineData("multiple", "config.value.notallowed", null)]
    public async Task OPS_BOOT_001_TheOrganizationCommandJudgesItsArgumentsAsBootstrapDoesAsync(
        string @case,
        string code,
        string? member)
    {
        List<string> arguments = ["organization", "--name", "Northwind Clinics", "--policy", _policy, "--reason", Reason];

        switch (@case)
        {
            case "unknown":
                arguments.AddRange(["--organization", "Northwind Clinics"]);
                break;
            case "twice":
                arguments.AddRange(["--name", "Contoso Pharmacies"]);
                break;
            case "valueless":
                arguments.Add("--multiplememberships");
                break;
            case "unprefixed":
                arguments[1] = "name";
                break;
            case "nameless":
                arguments.RemoveRange(1, 2);
                break;
            case "policyless":
                arguments.RemoveRange(3, 2);
                break;
            default:
                arguments.AddRange(["--multiplememberships", "yes"]);
                break;
        }

        Invocation run = await Invocation.TypedAsync(arguments);

        Assert.Equal(code, Reported.Refusal(run));

        if (member is not null)
        {
            Assert.Equal(member, Detail(run, "member"));
        }
    }

    /// <summary>
    /// OPS-BOOT-001 AC13 (D-248): a command line without <c>--reason</c> is refused with
    /// <c>config.change.reasonrequired</c> before the database is reached.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_AC13_ACommandLineWithoutAReasonIsRefusedAsync()
    {
        Invocation run = await Invocation.TypedAsync(["organization", "--name", "Northwind Clinics", "--policy", _policy]);

        Assert.Equal(ErrorCodes.ConfigurationChangeReasonRequired.ToString(), Reported.Refusal(run));
    }

    /// <summary>
    /// OPS-BOOT-001 AC13 and IDN-ORG-002 AC4 (D-248): the file is read as the policy
    /// route reads its policy object, before the database is reached: one that cannot
    /// be read, is not JSON or holds no object is refused naming <c>policy</c>; a member
    /// the object does not have, <c>emailDomains</c> and <c>reason</c> among them, is
    /// refused with <c>api.request.malformed</c> naming it; and a value a field does not
    /// take is refused with <c>config.value.notallowed</c> naming <c>policy</c>.
    /// </summary>
    /// <param name="written">What the file holds, or nothing where there is no file.</param>
    /// <param name="code">The refusal's code.</param>
    /// <param name="member">The member the refusal names.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(null, "api.request.malformed", "policy")]
    [InlineData("requiredAssurance: aal2", "api.request.malformed", "policy")]
    [InlineData("""["aal2"]""", "api.request.malformed", "policy")]
    [InlineData("""{ "emailDomains": ["northwind.example.test"] }""", "api.request.malformed", "emailDomains")]
    [InlineData("""{ "reason": "Production is set up." }""", "api.request.malformed", "reason")]
    [InlineData("""{ "requiredAssurance": "aal9" }""", "config.value.notallowed", "policy")]
    public async Task OPS_BOOT_001_AC13_APolicyFileThePolicyRouteWouldRefuseIsRefusedAsync(
        string? written,
        string code,
        string member)
    {
        if (written is null)
        {
            File.Delete(_policy);
        }
        else
        {
            await File.WriteAllTextAsync(_policy, written, TestContext.Current.CancellationToken);
        }

        Invocation run = await Invocation.TypedAsync(
            ["organization", "--name", "Northwind Clinics", "--policy", _policy, "--reason", Reason]);

        Assert.Equal(code, Reported.Refusal(run));
        Assert.Equal(member, Detail(run, "member"));
    }

    private static string? Detail(Invocation run, string name)
    {
        using var refusal = JsonDocument.Parse(run.Error);

        return refusal.RootElement.GetProperty("details").GetProperty(name).GetString();
    }
}
