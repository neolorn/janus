using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// A handler a deployment registered in the shipped one's place, keeping each admitted
/// message it was asked to carry so a test can read what reached it and what never did.
/// </summary>
internal sealed class NotificationHandlerInMemory : INotificationHandler
{
    /// <summary>
    /// Every admitted message the handler took, in order.
    /// </summary>
    public List<SendRequest> Taken { get; } = [];

    /// <summary>
    /// Those of them that went to an address.
    /// </summary>
    public IReadOnlyList<SendRequest> Mail =>
        [.. Taken.Where(one => one.Kind is SendKind.Email)];

    /// <summary>
    /// Those of them that went to a number.
    /// </summary>
    public IReadOnlyList<SendRequest> Texts =>
        [.. Taken.Where(one => one.Kind is SendKind.Sms)];

    /// <summary>
    /// How many times the handler was asked, whether or not it took the message.
    /// </summary>
    public int Asked { get; private set; }

    /// <summary>
    /// What the handler answers with instead of taking the message, where a test
    /// stands in for a transport that refuses.
    /// </summary>
    public Error? Refusal { get; set; }

    /// <summary>
    /// What a test does when the handler is asked, before it answers.
    /// </summary>
    public Action<SendRequest>? Asking { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result> SendAsync(SendRequest request, CancellationToken cancellationToken)
    {
        Asked++;
        Asking?.Invoke(request);

        if (Refusal is Error refused)
        {
            return ValueTask.FromResult(Result.Failure(refused));
        }

        Taken.Add(request);

        return ValueTask.FromResult(Result.Success());
    }
}
