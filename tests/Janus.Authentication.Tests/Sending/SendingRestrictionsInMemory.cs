using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The sending restrictions as an ask that sends nothing draws on them, keeping each
/// send drawn so a test can read what was counted in the place of a message. How the
/// restrictions judge it is tested where the sending path is.
/// </summary>
internal sealed class SendingRestrictionsInMemory : ISendingRestrictions
{
    /// <summary>
    /// Every send drawn, in order.
    /// </summary>
    public List<SendRequest> Drawn { get; } = [];

    /// <summary>
    /// What the restrictions answer with instead of counting the send, where a test
    /// stands in for a refusal.
    /// </summary>
    public Error? Refusal { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result> DrawAsync(SendRequest request, CancellationToken cancellationToken)
    {
        if (Refusal is Error refused)
        {
            return ValueTask.FromResult(Result.Failure(refused));
        }

        Drawn.Add(request);

        return ValueTask.FromResult(Result.Success());
    }
}
