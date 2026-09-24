using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Hosting.Events;

/// <summary>
/// One consumer registered for one event, bound to it.
/// </summary>
/// <param name="Name">What the consumer's taking of the event is recorded under.</param>
/// <param name="Handle">Offers the event to the consumer.</param>
/// <remarks>Implements LIB-API-001.</remarks>
internal sealed record EventConsumer(string Name, Func<CancellationToken, ValueTask<Result>> Handle);
