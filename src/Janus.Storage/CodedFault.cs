using System;
using Janus.Core;

namespace Janus.Storage;

/// <summary>
/// A fault that carries the code and the structured details of what broke, so that the
/// log, a job's failure and a command's exit name it.
/// </summary>
/// <remarks>
/// Implements CONV-ERR-001, CONV-CODE-007 and OPS-SEC-003 AC3 (D-183). It is never a
/// refusal: a request it reaches is answered <c>system.fault</c>, and no caller is
/// handed it as a result.
/// </remarks>
internal sealed class CodedFault : InvalidOperationException
{
    /// <summary>
    /// Makes a fault that names nothing more than that it is one.
    /// </summary>
    public CodedFault()
        : this(Error.From(ErrorCodes.SystemFault))
    {
    }

    /// <summary>
    /// Makes a fault that names nothing more than that it is one.
    /// </summary>
    /// <param name="message">What went wrong, for the developer reading a trace.</param>
    public CodedFault(string message)
        : base(message) => Failure = Error.From(ErrorCodes.SystemFault);

    /// <summary>
    /// Makes a fault that names nothing more than that it is one, over the fault that
    /// caused it.
    /// </summary>
    /// <param name="message">What went wrong, for the developer reading a trace.</param>
    /// <param name="innerException">The fault that caused it.</param>
    public CodedFault(string message, Exception innerException)
        : base(message, innerException) => Failure = Error.From(ErrorCodes.SystemFault);

    /// <summary>
    /// Makes the fault.
    /// </summary>
    /// <param name="failure">The code and the details it carries.</param>
    /// <exception cref="ArgumentNullException">The failure is absent.</exception>
    public CodedFault(Error failure)
        : base((failure ?? throw new ArgumentNullException(nameof(failure))).Code.ToString()) =>
        Failure = failure;

    /// <summary>
    /// The code and the details of what broke.
    /// </summary>
    public Error Failure { get; }
}
