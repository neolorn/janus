using System.Linq;
using System.Text.Json;
using Xunit;

namespace Janus.Cli.Tests;

/// <summary>
/// How every command reports what it did, as <c>bootstrap</c> does (OPS-SEC-001,
/// OPS-BOOT-001): exit code 0 with what it produced on standard output, or exit code 1
/// with one JSON line on standard error and nothing on standard output.
/// </summary>
internal static class Reported
{
    /// <summary>
    /// Holds a run to the report of a success: exit code 0 and nothing on standard
    /// error.
    /// </summary>
    /// <param name="run">The run.</param>
    public static void Success(Invocation run)
    {
        Assert.Equal(0, run.ExitCode);
        Assert.Empty(run.Error);
    }

    /// <summary>
    /// Holds a run to the report of a refusal: exit code 1, nothing on standard output,
    /// and on standard error one line, a JSON object of the code and its details and
    /// no other member.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <returns>The code the refusal carries.</returns>
    public static string Refusal(Invocation run)
    {
        Assert.Equal(1, run.ExitCode);
        Assert.Empty(run.Output);
        Assert.DoesNotContain('\n', run.Error.TrimEnd('\r', '\n'));

        using var refusal = JsonDocument.Parse(run.Error);

        Assert.Equal(["code", "details"], refusal.RootElement.EnumerateObject().Select(member => member.Name));
        Assert.Equal(JsonValueKind.Object, refusal.RootElement.GetProperty("details").ValueKind);

        string? code = refusal.RootElement.GetProperty("code").GetString();

        Assert.False(string.IsNullOrEmpty(code));

        return code;
    }
}
