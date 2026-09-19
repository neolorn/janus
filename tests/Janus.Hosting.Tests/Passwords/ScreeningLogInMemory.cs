using System.Collections.Generic;
using Janus.Authentication.Passwords;
using Janus.Core.Configuration;

namespace Janus.Hosting.Tests.Passwords;

/// <summary>
/// The screening log, holding what was recorded so a test can read it back.
/// </summary>
internal sealed class ScreeningLogInMemory : IScreeningLog
{
    private readonly List<(BlocklistSource Configured, BlocklistSource Used)> _entries = [];

    /// <summary>
    /// Every degradation recorded, in order.
    /// </summary>
    public IReadOnlyList<(BlocklistSource Configured, BlocklistSource Used)> Entries => _entries;

    /// <inheritdoc/>
    public void Degraded(BlocklistSource configured, BlocklistSource used) =>
        _entries.Add((configured, used));
}
