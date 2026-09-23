namespace Janus.Core.Configuration;

/// <summary>
/// An operation that works on any key of the catalogue, handed each one typed.
/// </summary>
/// <typeparam name="TResult">What the operation answers.</typeparam>
/// <remarks>
/// Implements CONV-CODE-004: the catalogue holds keys of many value types, and a
/// generic method reaches each with its own type where reflection would otherwise be
/// needed.
/// </remarks>
internal interface ISettingOperation<out TResult>
{
    /// <summary>
    /// Works on one key.
    /// </summary>
    /// <typeparam name="TValue">The type of the key's value.</typeparam>
    /// <param name="setting">The key.</param>
    /// <returns>What the operation answers.</returns>
    TResult On<TValue>(Setting<TValue> setting);
}
