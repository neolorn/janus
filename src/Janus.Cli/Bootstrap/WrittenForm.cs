using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Cli.Bootstrap;

/// <summary>
/// Reads a value named on the command line as its key admits it, and gives it back in
/// the form the settings table holds.
/// </summary>
/// <param name="entered">The value as the command line gave it.</param>
/// <remarks>
/// Implements OPS-BOOT-001 and chapter 10 section 4 value types. A value is written in
/// the form the wire contract writes it in, a JSON array for a list, so the table
/// holds exactly what a read of it would accept.
/// </remarks>
internal sealed class WrittenForm(string entered) : ISettingOperation<Result<string>>
{
    /// <inheritdoc/>
    public Result<string> On<TValue>(Setting<TValue> setting) =>
        setting.Read(entered).Match(value => Result.Success(setting.Write(value)), Result.Failure<string>);
}
