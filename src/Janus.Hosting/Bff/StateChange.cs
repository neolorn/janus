using System;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Bff;

/// <summary>
/// Whether a request changes state, which is what the CSRF layers are required on.
/// </summary>
/// <remarks>
/// Implements BFF-CSRF-001. The safe methods of RFC 9110 section 9.2.1 change
/// nothing, so everything else does, including a method the framework does not know:
/// a request is protected unless it is one of the four.
/// </remarks>
internal static class StateChange
{
    /// <summary>
    /// Whether the request changes state.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>Whether it does.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public static bool Changes(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return !(HttpMethods.IsGet(request.Method)
            || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method)
            || HttpMethods.IsTrace(request.Method));
    }
}
