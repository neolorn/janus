using System;

namespace Janus.Core;

/// <summary>
/// The outcome of an operation that either produced a value or failed in a way the
/// specification expects. A fault throws instead.
/// </summary>
/// <typeparam name="TValue">The type the operation produces when it succeeds.</typeparam>
/// <remarks>
/// Implements CONV-DESIGN-005 and CONV-ERR-001. The value is reachable only through
/// <see cref="Match{TOutcome}"/> and <see cref="Switch"/>, so a caller cannot read it
/// without handling the failure. An operation never returns null to mean not found; it
/// returns a failure carrying the named code.
/// </remarks>
public readonly record struct Result<TValue>
{
    private readonly TValue _value;
    private readonly Error? _failure;
    private readonly bool _succeeded;

    private Result(bool succeeded, TValue value, Error? failure)
    {
        _succeeded = succeeded;
        _value = value;
        _failure = failure;
    }

    /// <summary>
    /// Produces a value from whichever outcome occurred.
    /// </summary>
    /// <typeparam name="TOutcome">The type both branches produce.</typeparam>
    /// <param name="onSuccess">Called with the value when the operation succeeded.</param>
    /// <param name="onFailure">Called with the failure when it did not.</param>
    /// <returns>Whatever the branch that ran produced.</returns>
    /// <exception cref="ArgumentNullException">Either branch is absent.</exception>
    /// <exception cref="InvalidOperationException">The outcome was never set.</exception>
    public TOutcome Match<TOutcome>(Func<TValue, TOutcome> onSuccess, Func<Error, TOutcome> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (_succeeded)
        {
            return onSuccess(_value);
        }

        return onFailure(Failed());
    }

    /// <summary>
    /// Runs whichever branch matches the outcome.
    /// </summary>
    /// <param name="onSuccess">Called with the value when the operation succeeded.</param>
    /// <param name="onFailure">Called with the failure when it did not.</param>
    /// <exception cref="ArgumentNullException">Either branch is absent.</exception>
    /// <exception cref="InvalidOperationException">The outcome was never set.</exception>
    public void Switch(Action<TValue> onSuccess, Action<Error> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (_succeeded)
        {
            onSuccess(_value);
            return;
        }

        onFailure(Failed());
    }

    internal static Result<TValue> FromValue(TValue value) => new(succeeded: true, value, failure: null);

    internal static Result<TValue> FromError(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result<TValue>(succeeded: false, default!, error);
    }

    /// <summary>
    /// The failure of an outcome that did not succeed. An outcome that was never set
    /// is a fault, for the reason given on the non-generic result.
    /// </summary>
    private Error Failed() => _failure ?? throw new InvalidOperationException("The outcome was never set.");
}
