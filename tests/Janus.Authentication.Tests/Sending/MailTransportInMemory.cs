using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The mail transport, keeping every payload it was handed so a test can read the
/// exact field set the one mapping site built.
/// </summary>
internal sealed class MailTransportInMemory : IMailTransport
{
    /// <summary>
    /// Every mail handed over, in order.
    /// </summary>
    public List<MailMessage> Taken { get; } = [];

    /// <summary>
    /// Whether the transport takes what it is handed.
    /// </summary>
    public bool Accepts { get; set; } = true;

    /// <summary>
    /// How many mails the transport takes before it refuses the rest.
    /// </summary>
    public int Takes { get; set; } = int.MaxValue;

    /// <summary>
    /// What a test runs as each mail is handed over, where it watches for the moment.
    /// </summary>
    public Action? Handed { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result> SendAsync(MailMessage mail, CancellationToken cancellationToken)
    {
        Handed?.Invoke();

        if (!Accepts || Taken.Count >= Takes)
        {
            return ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.SystemFault)));
        }

        Taken.Add(mail);

        return ValueTask.FromResult(Result.Success());
    }
}
