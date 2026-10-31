using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The host's notification handler, keeping each message it takes on the view of its
/// channel, so a test reads a code or a link as the value the message carries.
/// </summary>
internal sealed class NotificationsInMemory : INotificationHandler
{
    /// <summary>
    /// What the handler took for an address.
    /// </summary>
    public ChannelInMemory Mail { get; } = new();

    /// <summary>
    /// What the handler took for a number.
    /// </summary>
    public ChannelInMemory Sms { get; } = new();

    /// <inheritdoc/>
    public ValueTask<Result> SendAsync(SendRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult((request.Channel is SendKind.Email ? Mail : Sms).Take(request));
}
