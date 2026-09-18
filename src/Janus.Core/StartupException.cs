using System;

namespace Janus.Core;

/// <summary>
/// A fault that stops the library starting: a value the deployment has to name and
/// did not, or a value outside what its key admits. Configuration faults surface here
/// rather than at the first request.
/// </summary>
/// <remarks>Implements CONV-ERR-001, OPS-CFG-003, LIB-HOST-001.</remarks>
public sealed class StartupException : Exception
{
    /// <summary>
    /// A fault with a message and no code.
    /// </summary>
    public StartupException()
    {
    }

    /// <summary>
    /// A fault with a message.
    /// </summary>
    /// <param name="message">What the operator has to fix.</param>
    public StartupException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// A fault raised while handling another.
    /// </summary>
    /// <param name="message">What the operator has to fix.</param>
    /// <param name="innerException">The fault underneath.</param>
    public StartupException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// A fault carrying the failure the check produced, so the code and the structured
    /// context reach the operator unchanged.
    /// </summary>
    /// <param name="message">What the operator has to fix.</param>
    /// <param name="failure">The failure, with its code and context.</param>
    public StartupException(string message, Error failure)
        : base(message) => Failure = failure;

    /// <summary>
    /// The failure the check produced, where the specification names a code for it.
    /// </summary>
    public Error? Failure { get; }
}
