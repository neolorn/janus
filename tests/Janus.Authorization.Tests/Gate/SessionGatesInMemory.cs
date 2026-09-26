using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Core;

namespace Janus.Authorization.Tests.Gate;

/// <summary>
/// The library's own session, held in memory: the one person it belongs to, and the
/// gates it has proved enough for.
/// </summary>
/// <param name="holder">Whose session carries the request.</param>
internal sealed class SessionGatesInMemory(SubjectId holder) : ISessionGates
{
    private readonly HashSet<string> _met = [];

    /// <summary>
    /// The gates asked about, in order.
    /// </summary>
    public List<string> Asked { get; } = [];

    /// <summary>
    /// Records that the session has proved what a gate costs.
    /// </summary>
    /// <param name="gate">The gate's name.</param>
    public void Meets(string gate) => _met.Add(gate);

    /// <inheritdoc/>
    public bool Judges(AccessContext context) => context.Acting == holder;

    /// <inheritdoc/>
    public ValueTask<Error?> OutstandingAsync(
        AccessContext context,
        string gate,
        CancellationToken cancellationToken)
    {
        Asked.Add(gate);

        return ValueTask.FromResult(_met.Contains(gate)
            ? null
            : Error.From(ErrorCodes.StepUpRequired, "action", JsonSerializer.SerializeToElement(gate)));
    }
}
