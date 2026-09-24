using Janus.Authentication.Configuration;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Cli.Configuration;

/// <summary>
/// Reads a value named on the command line for a protected key that exists once, as
/// its key admits it.
/// </summary>
/// <param name="entered">The value as the command line gave it.</param>
/// <remarks>Implements OPS-CFG-004 and the chapter 10 section 4 value types.</remarks>
internal sealed class ProtectedReading(string entered) : ISettingOperation<Result<ProtectedValue>>
{
    /// <inheritdoc/>
    public Result<ProtectedValue> On<TValue>(Setting<TValue> setting) =>
        setting.Read(entered).Match(
            value => Result.Success<ProtectedValue>(new ProtectedSettingValue<TValue>(setting, value)),
            Result.Failure<ProtectedValue>);
}
