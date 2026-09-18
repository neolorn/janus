using System;

namespace Janus.Core;

/// <summary>
/// The outcome of an operation that either succeeded or failed in a way the
/// specification expects. A fault throws instead.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-005 and CONV-ERR-001. The outcome is reachable only through
/// <see cref="Match{TOutcome}"/> and <see cref="Switch"/>, so a caller cannot read a
/// value without handling the failure.
/// </remarks>
public readonly record struct Result
{
    private readonly Error? _failure;
    private readonly bool _succeeded;

    private Result(bool succeeded, Error? failure)
    {
        _succeeded = succeeded;
        _failure = failure;
    }

    /// <summary>
    /// The operation succeeded.
    /// </summary>
    /// <returns>A successful outcome.</returns>
    public static Result Success() => new(succeeded: true, failure: null);

    /// <summary>
    /// The operation failed in a way the specification expects.
    /// </summary>
    /// <param name="error">The failure.</param>
    /// <returns>A failed outcome.</returns>
    /// <exception cref="ArgumentNullException">The failure is absent.</exception>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result(succeeded: false, failure: error);
    }

    /// <summary>
    /// The operation succeeded and carries a value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>A successful outcome carrying the value.</returns>
    public static Result<TValue> Success<TValue>(TValue value) => Result<TValue>.FromValue(value);

    /// <summary>
    /// The operation that would have produced a value failed instead.
    /// </summary>
    /// <typeparam name="TValue">The type the operation would have produced.</typeparam>
    /// <param name="error">The failure.</param>
    /// <returns>A failed outcome.</returns>
    public static Result<TValue> Failure<TValue>(Error error) => Result<TValue>.FromError(error);

    /// <summary>
    /// Produces a value from whichever outcome occurred.
    /// </summary>
    /// <typeparam name="TOutcome">The type both branches produce.</typeparam>
    /// <param name="onSuccess">Called when the operation succeeded.</param>
    /// <param name="onFailure">Called with the failure when it did not.</param>
    /// <returns>Whatever the branch that ran produced.</returns>
    /// <exception cref="ArgumentNullException">Either branch is absent.</exception>
    /// <exception cref="InvalidOperationException">The outcome was never set.</exception>
    public TOutcome Match<TOutcome>(Func<TOutcome> onSuccess, Func<Error, TOutcome> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (_succeeded)
        {
            return onSuccess();
        }

        return onFailure(Failed());
    }

    /// <summary>
    /// Runs whichever branch matches the outcome.
    /// </summary>
    /// <param name="onSuccess">Called when the operation succeeded.</param>
    /// <param name="onFailure">Called with the failure when it did not.</param>
    /// <exception cref="ArgumentNullException">Either branch is absent.</exception>
    /// <exception cref="InvalidOperationException">The outcome was never set.</exception>
    public void Switch(Action onSuccess, Action<Error> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (_succeeded)
        {
            onSuccess();
            return;
        }

        onFailure(Failed());
    }

    /// <summary>
    /// The failure of an outcome that did not succeed. An outcome that was never set
    /// is a fault: a default instance is neither a success nor a failure, and reading
    /// it as either would be the fail-open case CONV-ERR-002 exists to prevent.
    /// </summary>
    private Error Failed() => _failure ?? throw new InvalidOperationException("The outcome was never set.");
}
