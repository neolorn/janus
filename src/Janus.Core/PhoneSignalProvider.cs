using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What a deployment registers to answer for a number: a callback that takes the
/// number in canonical form and says what the carrier reports about it.
/// </summary>
/// <param name="Signal">What is known about this number.</param>
/// <remarks>
/// Implements AUTH-FACT-002b and LIB-HOST-001. The library ships no gateway: which
/// provider answers, and what it costs to ask, is the deployment's, and where none is
/// registered the record says so.
/// </remarks>
public sealed record PhoneSignalProvider(
    Func<string, CancellationToken, ValueTask<PhoneSignal>> Signal);
