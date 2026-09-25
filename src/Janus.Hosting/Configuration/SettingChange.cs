using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Configuration;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Configuration;

/// <summary>
/// Putting a received value in force for one key, whatever the type of its value,
/// through the one operation a runtime setting is written through.
/// </summary>
/// <param name="administration">The one operation a runtime setting is written through.</param>
/// <param name="value">What the key becomes, as the request carried it.</param>
/// <param name="reason">Why.</param>
/// <param name="challenge">What the <c>config:loosen</c> gate answered.</param>
/// <param name="context">Who is asking.</param>
/// <param name="cancellationToken">Abandons the change.</param>
/// <remarks>Implements OPS-CFG-002, OPS-CFG-003 and OPS-CFG-005.</remarks>
internal sealed class SettingChange(
    ConfigurationAdministration administration,
    JsonElement value,
    string reason,
    StepUpChallenge challenge,
    AccessContext context,
    CancellationToken cancellationToken) : ISettingOperation<ValueTask<Result>>
{
    /// <inheritdoc/>
    public async ValueTask<Result> On<TValue>(Setting<TValue> setting)
    {
        Error? failure = null;

        TValue received = setting.Received(value)
            .Match(one => one, error => Held<TValue>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        return await administration
            .ChangeAsync(setting, received, reason, challenge, context, cancellationToken)
            .ConfigureAwait(false);
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
