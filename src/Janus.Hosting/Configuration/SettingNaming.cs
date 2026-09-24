using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Configuration;

/// <summary>
/// Whether the deployment named a value for one key, whatever the type of its value.
/// </summary>
/// <param name="configuration">Where the value in force is read.</param>
/// <param name="cancellationToken">Abandons the read.</param>
/// <remarks>
/// Implements LIB-HOST-001. A key with no default reads as undeclared until the
/// deployment names it; any other failure is a value that was named and cannot be
/// read, which the caller is told of rather than counting it as either.
/// </remarks>
internal sealed class SettingNaming(
    IConfigurationStore configuration,
    CancellationToken cancellationToken) : ISettingOperation<ValueTask<Result<bool>>>
{
    /// <inheritdoc/>
    public async ValueTask<Result<bool>> On<TValue>(Setting<TValue> setting) =>
        (await configuration.ReadAsync(setting, cancellationToken).ConfigureAwait(false)).Match(
            _ => Result.Success(true),
            failure => failure.Code == ErrorCodes.StartupDeclarationMissing
                ? Result.Success(false)
                : Result.Failure<bool>(failure));
}
