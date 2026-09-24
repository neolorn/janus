using System.Collections.Generic;
using Janus.Authentication.Configuration;

namespace Janus.Cli.Configuration;

/// <summary>
/// What the <c>configure</c> command is asked to put in force, and why.
/// </summary>
/// <param name="Values">What each protected key named becomes, in the order named.</param>
/// <param name="Reason">Why, where the command line gave one.</param>
internal sealed record ConfigureRequest(IReadOnlyList<ProtectedValue> Values, string? Reason);
