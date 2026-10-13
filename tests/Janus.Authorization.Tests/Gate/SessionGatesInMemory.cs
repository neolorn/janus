using System;
using System.Collections.Generic;
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

    private readonly Dictionary<string, Core.Gate> _costs = new(StringComparer.Ordinal);

    /// <summary>
    /// The gates asked about, in order.
    /// </summary>
    public List<string> Asked { get; } = [];

    /// <summary>
    /// Records that the session has proved what a gate costs.
    /// </summary>
    /// <param name="gate">The gate's name.</param>
    public void Meets(string gate) => _met.Add(gate);

    /// <summary>
    /// Records what a gate costs under the acting person's policy.
    /// </summary>
    /// <param name="gate">The gate's name.</param>
    /// <param name="cost">Its three values.</param>
    public void Costs(string gate, Core.Gate cost) => _costs[gate] = cost;

    /// <inheritdoc/>
    public bool Judges(AccessContext context) => context.Acting == holder;

    /// <inheritdoc/>
    public ValueTask<Error?> OutstandingAsync(
        AccessContext context,
        string gate,
        CancellationToken cancellationToken)
    {
        Asked.Add(gate);

        return ValueTask.FromResult(_met.Contains(gate) ? null : Error.From(ErrorCodes.StepUpRequired));
    }

    /// <inheritdoc/>
    public ValueTask<CapabilityResidual?> ResidualAsync(
        AccessContext context,
        string gate,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<CapabilityResidual?>(_met.Contains(gate) ? null : CapabilityResidual.StepUp);

    /// <inheritdoc/>
    public ValueTask<Result<Core.Gate>> CostAsync(
        AccessContext context,
        string gate,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_costs.TryGetValue(gate, out Core.Gate? cost)
            ? Result.Success(cost)
            : Result.Failure<Core.Gate>(Error.From(ErrorCodes.StepUpRequired)));
}
