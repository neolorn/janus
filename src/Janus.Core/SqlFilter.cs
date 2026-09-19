using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// The permission predicate as a fragment a hand-written query composes into its
/// <c>WHERE</c> clause, with every value carried as a parameter.
/// </summary>
/// <param name="Text">The fragment, naming the parameters and interpolating no value.</param>
/// <param name="Parameters">The values the fragment names, by parameter name.</param>
/// <remarks>
/// Implements AUTHZ-GATE-002 and AUTHZ-GATE-003. The fragment is PostgreSQL and the
/// contract claims no dialect portability (LIB-API-004).
/// </remarks>
public sealed record SqlFilter(string Text, IReadOnlyDictionary<string, object> Parameters);
