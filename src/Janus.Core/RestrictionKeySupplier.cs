using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What a host registers for a <c>host:&lt;name&gt;</c> restriction key: the name
/// after the colon, and the callback that answers the value the restriction's
/// buckets are counted under for one send.
/// </summary>
/// <param name="Name">The name after the colon.</param>
/// <param name="Key">
/// What the send counts against, answered once per send the restriction applies to.
/// </param>
/// <remarks>
/// Implements LIB-HOST-001 and AUTH-ABUSE-004. A restriction naming a key with no
/// registered supplier fails startup; the library never guesses a value for one.
/// </remarks>
public sealed record RestrictionKeySupplier(
    string Name,
    Func<SendContext, CancellationToken, ValueTask<string>> Key);
