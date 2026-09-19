using System.Collections.Generic;
using Janus.Authentication.Passwords;
using Janus.Core.Configuration;

namespace Janus.Authentication.Tests.Passwords;

/// <summary>
/// The screening log, recording what it was told.
/// </summary>
internal sealed class ScreeningLogInMemory : IScreeningLog
{
    /// <summary>
    /// Each fall back to another corpus, as the pair of corpora it was between.
    /// </summary>
    public List<(BlocklistSource Configured, BlocklistSource Used)> Degradations { get; } = [];

    /// <inheritdoc/>
    public void Degraded(BlocklistSource configured, BlocklistSource used) =>
        Degradations.Add((configured, used));
}
