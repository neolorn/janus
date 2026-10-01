using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// What the step-up gate bound to an action still asks of the caller's session.
/// </summary>
/// <param name="sessions">
/// Where the library's own session is judged against a gate, and where the gate's values
/// are read from the acting person's policy, which a deployment whose people sign in
/// through this library has and one consuming authorization alone does not.
/// </param>
/// <param name="assurance">
/// Where how far the caller's session has authenticated is read, which a deployment
/// consuming authorization without this library's authentication supplies, and which a
/// deployment requiring no step-up leaves absent.
/// </param>
/// <param name="time">The clock a reported proof's recency is judged against.</param>
/// <remarks>
/// Implements AUTHZ-GATE-005, AUTH-STEP-001, AUTH-STEP-002, AUTH-STEP-003 and
/// LIB-HOST-004. An action bound to no gate asks nothing of the session; a bound one is
/// met by what the acting person's session has proved and by nothing else.
/// </remarks>
internal sealed class StepUpGates(
    ISessionGates? sessions,
    IAssuranceProvider? assurance,
    TimeProvider time)
{
    private static readonly JsonSerializerOptions Names =
        new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>
    /// What a named gate still requires, or nothing where no gate is named.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="gate">The gate's name, or nothing.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The refusal, or nothing where nothing is outstanding.</returns>
    public async ValueTask<Error?> OutstandingAsync(
        AccessContext context,
        string? gate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (gate is null)
        {
            return null;
        }

        // AUTH-STEP-002: a gate is judged against the session record, so where the
        // acting person's own session carries the request it is that session's proof
        // that meets the gate or does not.
        if (sessions is not null && sessions.Judges(context))
        {
            return await sessions.OutstandingAsync(context, gate, cancellationToken).ConfigureAwait(false);
        }

        // AUTH-STEP-003: nothing reports what this caller proved, so the gate is unmet,
        // and the code tells a deployment that can ask for step-up from one that cannot
        // ask at all (LIB-HOST-004).
        if (assurance is null)
        {
            return Error.From(ErrorCodes.StepUpUnavailable);
        }

        // LIB-HOST-004: the gate costs what it costs a session under the acting
        // person's policy, and nothing that cannot read that policy meets it.
        if (sessions is null)
        {
            return Error.From(ErrorCodes.StepUpRequired);
        }

        Error? failure = null;

        Core.Gate cost = (await sessions.CostAsync(context, gate, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Core.Gate>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        // A report that cannot be read is a gate unmet, never one assumed met.
        AttainedAssurance? attained = (await assurance.AttainedAsync(context, cancellationToken).ConfigureAwait(false))
            .Match<AttainedAssurance?>(value => value, _ => null);

        AssuranceLevel required = Required(cost, attained);

        return attained is not null && Meets(attained, cost, required, time.GetUtcNow())
            ? null
            : Refusal(cost, required);
    }

    // AUTH-STEP-002a: a gate asking for what the account can reach asks for what the
    // report says it can reach, and never for less than one factor.
    private static AssuranceLevel Required(Core.Gate cost, AttainedAssurance? attained) =>
        cost.Level switch
        {
            GateLevel.Aal1 => AssuranceLevel.Aal1,
            GateLevel.Aal2 => AssuranceLevel.Aal2,
            _ => attained is { Reachable: > AssuranceLevel.Aal1 } report ? report.Reachable : AssuranceLevel.Aal1,
        };

    private static bool Meets(AttainedAssurance attained, Core.Gate cost, AssuranceLevel required, DateTimeOffset now) =>
        attained.Level >= required
        && (!cost.PhishingResistant || attained.PhishingResistant)
        && now - attained.AttainedAt <= cost.MaximumAge;

    // Chapter 09: every auth.stepup.required carries the gate, the outcome and the
    // combinations; a host's report offers none, since the host's own sign-in is what
    // the person steps up at.
    private static Error Refusal(Core.Gate cost, AssuranceLevel required)
    {
        var gate = new Dictionary<string, JsonElement>(capacity: 3, StringComparer.Ordinal)
        {
            ["level"] = JsonSerializer.SerializeToElement(Written(required)),
            ["phishingResistant"] = JsonSerializer.SerializeToElement(cost.PhishingResistant),
            ["maxAge"] = JsonSerializer.SerializeToElement((long)cost.MaximumAge.TotalSeconds),
        };

        var details = new Dictionary<string, JsonElement>(capacity: 4, StringComparer.Ordinal)
        {
            ["required"] = JsonSerializer.SerializeToElement(gate),
            ["outcome"] = JsonSerializer.SerializeToElement("present"),
            ["options"] = JsonSerializer.SerializeToElement(Array.Empty<string[]>()),
            ["pendingUntil"] = JsonSerializer.SerializeToElement<DateTimeOffset?>(null),
        };

        return new Error(ErrorCodes.StepUpRequired, details);
    }

    private static string Written(AssuranceLevel level) =>
        JsonSerializer.Serialize(level, Names).Trim('"');

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
