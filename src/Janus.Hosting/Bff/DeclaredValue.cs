using System;

namespace Janus.Hosting.Bff;

/// <summary>
/// One route or query value an endpoint binds to a type: its name in the request, the
/// type, and whether a request's text reads as it.
/// </summary>
/// <param name="Name">The name the route pattern or the query gives the value.</param>
/// <param name="Type">The type the handler takes it as.</param>
/// <param name="Reads">Whether a request's text, or its absence, reads as the type.</param>
/// <remarks>
/// Implements CONV-DESIGN-006. The reading is the type's own, captured where the
/// endpoint declares the value, so nothing reads the handler to find it.
/// </remarks>
internal sealed record DeclaredValue(string Name, Type Type, Func<string?, bool> Reads);
