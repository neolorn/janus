using System.Collections.Generic;
using Janus.Core;

namespace Janus.Privacy.Tests;

/// <summary>
/// A host's handler for consent and objection changes, which the library verifies
/// the registration of and never calls.
/// </summary>
/// <param name="purposes">The purposes it does the work for.</param>
internal sealed class PurposeHandlerInMemory(params string[] purposes) : IPurposeHandler
{
    /// <inheritdoc/>
    public IReadOnlyCollection<string> Purposes => purposes;
}
