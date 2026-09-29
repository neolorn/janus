using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Xunit;

namespace Janus.Cli.Tests;

/// <summary>
/// What the executable answers an invocation that names no command it carries
/// (OPS-SEC-001, API-CONV-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class CommandTests
{
    /// <summary>
    /// OPS-SEC-001: a command the executable does not carry is refused with one JSON line
    /// on standard error, <c>api.request.malformed</c> naming the command, and exit code
    /// 1; a name differing only in case is another command.
    /// </summary>
    /// <param name="command">The command named.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("rotate-everything")]
    [InlineData("Bootstrap")]
    public async Task OPS_SEC_001_ACommandTheExecutableDoesNotCarryIsRefusedAsync(string command)
    {
        Invocation run = await Invocation.TypedAsync([command, "--reason", "none"]);

        using var refusal = JsonDocument.Parse(run.Error);

        Assert.Equal(1, run.ExitCode);
        Assert.Empty(run.Output);
        Assert.Single(run.Error.TrimEnd().Split('\n'));
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), refusal.RootElement.GetProperty("code").GetString());
        Assert.Equal(command, refusal.RootElement.GetProperty("details").GetProperty("member").GetString());
    }

    /// <summary>
    /// OPS-SEC-001 and API-CONV-002: an invocation that names no command at all failed
    /// before any member was read, so its refusal carries the code alone.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_001_AnInvocationNamingNoCommandIsRefusedAsync()
    {
        Invocation run = await Invocation.TypedAsync([]);

        using var refusal = JsonDocument.Parse(run.Error);

        Assert.Equal(1, run.ExitCode);
        Assert.Empty(run.Output);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), refusal.RootElement.GetProperty("code").GetString());
        Assert.Empty(refusal.RootElement.GetProperty("details").EnumerateObject());
    }
}
