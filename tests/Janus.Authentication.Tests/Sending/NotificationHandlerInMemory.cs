using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// What carries the library's messages, keeping each request so a test can read which
/// message went where, in which language, with what in it. The words, the channel's
/// payload and the delivery are the handler's and are tested where the handler is.
/// </summary>
internal sealed class NotificationHandlerInMemory : INotificationHandler
{
    // A reference stands for what a gateway would correlate by; one generator for
    // the run is enough to keep two of them apart.
    private static readonly RandomNumberGenerator Randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// Every message taken, in order.
    /// </summary>
    public List<SendRequest> Sent { get; } = [];

    /// <summary>
    /// Those of them that went to an address.
    /// </summary>
    public IReadOnlyList<SendRequest> Mail =>
        [.. Sent.Where(one => one.Kind is SendKind.Email)];

    /// <summary>
    /// Those of them that went to a number.
    /// </summary>
    public IReadOnlyList<SendRequest> Texts =>
        [.. Sent.Where(one => one.Kind is SendKind.Sms)];

    /// <summary>
    /// What the handler answers with instead of taking the message, where a test
    /// stands in for a refusal.
    /// </summary>
    public Error? Refusal { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<SendReference>> SendAsync(
        SendRequest request,
        CancellationToken cancellationToken)
    {
        if (Refusal is Error refused)
        {
            return ValueTask.FromResult(Result.Failure<SendReference>(refused));
        }

        Sent.Add(request);

        return ValueTask.FromResult(Result.Success(SendReference.Draw(Randomness)));
    }
}
