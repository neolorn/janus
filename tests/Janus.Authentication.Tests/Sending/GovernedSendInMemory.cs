using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The governed send as an operation meets it, keeping each message undertaken so a
/// test can read which message went where, under which purpose, in which language,
/// with what in it. How a message is judged, counted and carried is tested where the
/// sending path is.
/// </summary>
internal sealed class GovernedSendInMemory : IGovernedSend, IFollowedSend
{
    // A reference stands for what a gateway would correlate by; one generator for
    // the run is enough to keep two of them apart.
    private static readonly RandomNumberGenerator Randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// The unit of work the operations under test run in. Where a test names it, a send
    /// undertaken outside it is a fault, as it is in the library, and a message counts
    /// as carried only once the unit of work it was undertaken in has committed.
    /// </summary>
    public UnitOfWorkInMemory? Work { get; set; }

    /// <summary>
    /// Every message admitted, in order, whether or not its unit of work then committed.
    /// </summary>
    public List<OutboundMessage> Sent { get; } = [];

    /// <summary>
    /// Those of them whose unit of work committed, which are the ones a transport would
    /// be asked to carry.
    /// </summary>
    public List<OutboundMessage> Carried { get; } = [];

    /// <summary>
    /// Those admitted that went to an address.
    /// </summary>
    public IReadOnlyList<OutboundMessage> Mail =>
        [.. Sent.Where(one => one.Kind is SendKind.Email)];

    /// <summary>
    /// Those admitted that went to a number.
    /// </summary>
    public IReadOnlyList<OutboundMessage> Texts =>
        [.. Sent.Where(one => one.Kind is SendKind.Sms)];

    /// <summary>
    /// What the send answers with instead of admitting the message, where a test
    /// stands in for a refusal.
    /// </summary>
    public Error? Refusal { get; set; }

    /// <summary>
    /// Whether the attempt that follows a commit carries what was admitted, which a
    /// caller that follows its sends asks; false where a test stands in for a transport
    /// that refuses.
    /// </summary>
    public bool Carries { get; set; } = true;

    /// <inheritdoc/>
    public ValueTask<Result<SendReference>> UndertakeAsync(
        OutboundMessage message,
        CancellationToken cancellationToken)
    {
        if (Work is { Open: false })
        {
            throw new InvalidOperationException("A send is undertaken inside the caller's unit of work.");
        }

        if (Refusal is Error refused)
        {
            return ValueTask.FromResult(Result.Failure<SendReference>(refused));
        }

        Sent.Add(message);

        if (Work is null)
        {
            Carried.Add(message);
        }
        else
        {
            Work.AfterCommit(_ =>
                {
                    Carried.Add(message);

                    return ValueTask.CompletedTask;
                })
                .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
        }

        return ValueTask.FromResult(Result.Success(SendReference.Draw(Randomness)));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<SendDeliveryId>>> AdmitAsync(
        OutboundMessage message,
        CancellationToken cancellationToken) =>
        (await UndertakeAsync(message, cancellationToken)).Match(
            _ => Result.Success<IReadOnlyList<SendDeliveryId>>([SendDeliveryId.Of(DateTimeOffset.UnixEpoch)]),
            Result.Failure<IReadOnlyList<SendDeliveryId>>);

    /// <inheritdoc/>
    public ValueTask<bool> CarriedAsync(
        IReadOnlyList<SendDeliveryId> admitted,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Carries);
}
