using System;
using System.Collections.Generic;
using System.Text;
using Janus.Core;

namespace Janus.Identity.Preferences;

/// <summary>
/// What an account has settled about how it is addressed and how the applications of
/// the deployment present themselves: the language, the time zone and the values of the
/// keys the host declared.
/// </summary>
/// <remarks>
/// Implements IDN-ATTR-001 and REG-PREF-001. The library validates a value against the
/// declaration of its key and never branches on the value, because the set differs per
/// host and understanding it is not the library's business. The language and the time
/// zone are the library's own: notifications and account pages are written in one and
/// rendered in the other.
/// </remarks>
internal sealed class PreferenceSet
{
    private readonly Dictionary<string, string> _values;

    private PreferenceSet(
        SubjectId subject,
        string? language,
        string? timeZone,
        Dictionary<string, string> values)
    {
        Subject = subject;
        Language = language;
        TimeZone = timeZone;
        _values = values;
    }

    /// <summary>
    /// Whose preferences these are.
    /// </summary>
    public SubjectId Subject { get; }

    /// <summary>
    /// The language the person reads, as a BCP 47 tag, where the account has one. A
    /// message sent without a request resolves its language from here.
    /// </summary>
    public string? Language { get; private set; }

    /// <summary>
    /// The zone the person's times are rendered in, as an IANA zone identifier.
    /// </summary>
    public string? TimeZone { get; private set; }

    /// <summary>
    /// The values the account has set, by the key the host declared them under.
    /// </summary>
    public IReadOnlyDictionary<string, string> Values => _values;

    /// <summary>
    /// The size of the set: its keys and its values as UTF-8 bytes, which is what
    /// <c>preferences.maxsize</c> bounds.
    /// </summary>
    public int Size
    {
        get
        {
            int size = 0;

            foreach (KeyValuePair<string, string> value in _values)
            {
                size += Encoding.UTF8.GetByteCount(value.Key) + Encoding.UTF8.GetByteCount(value.Value);
            }

            return size;
        }
    }

    /// <summary>
    /// The preferences of an account that has settled nothing.
    /// </summary>
    /// <param name="subject">Whose they are.</param>
    /// <returns>The set.</returns>
    public static PreferenceSet Empty(SubjectId subject) => new(subject, null, null, []);

    /// <summary>
    /// The preferences as they already stand. This is the store's translation of a
    /// stored row and no change the account made.
    /// </summary>
    /// <param name="subject">Whose they are.</param>
    /// <param name="language">The language, where one is held.</param>
    /// <param name="timeZone">The time zone, where one is held.</param>
    /// <param name="values">The values the account has set.</param>
    /// <returns>The set.</returns>
    /// <exception cref="ArgumentNullException">The values are absent.</exception>
    public static PreferenceSet Existing(
        SubjectId subject,
        string? language,
        string? timeZone,
        IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new PreferenceSet(
            subject,
            language,
            timeZone,
            new Dictionary<string, string>(values, StringComparer.Ordinal));
    }

    /// <summary>
    /// Sets or clears the language the person reads. Registration sets it from the
    /// locale of the request, so a message in the wrong language is the exception.
    /// </summary>
    /// <param name="language">The BCP 47 tag, or nothing to clear it.</param>
    public void SetLanguage(string? language) => Language = language;

    /// <summary>
    /// Sets or clears the zone the person's times are rendered in.
    /// </summary>
    /// <param name="timeZone">The IANA zone identifier, or nothing to clear it.</param>
    public void SetTimeZone(string? timeZone) => TimeZone = timeZone;

    /// <summary>
    /// The value in force for a key: the one the account set, or the one the host
    /// declared as the default.
    /// </summary>
    /// <param name="declaration">The key's declaration.</param>
    /// <returns>The value in force.</returns>
    /// <exception cref="ArgumentNullException">The declaration is absent.</exception>
    public string InForce(PreferenceDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        return _values.TryGetValue(declaration.Name, out string? value) ? value : declaration.Default;
    }

    /// <summary>
    /// Sets one preference.
    /// </summary>
    /// <param name="declarations">The keys the host declared.</param>
    /// <param name="name">The key.</param>
    /// <param name="value">The value, in the spelling its declared type takes.</param>
    /// <param name="asAdministrator">Whether an administrator is setting it.</param>
    /// <param name="maximumSize">What <c>preferences.maxsize</c> allows the set.</param>
    /// <returns>Success, or the refusal and its code.</returns>
    /// <exception cref="ArgumentNullException">The declarations, key or value are absent.</exception>
    public Result Set(
        PreferenceDeclarations declarations,
        string name,
        string value,
        bool asAdministrator,
        int maximumSize)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        if (!declarations.TryFind(name, out PreferenceDeclaration declaration))
        {
            return Result.Failure(Error.From(ErrorCodes.PreferenceUndeclared));
        }

        if (declaration.AdministratorOnly && !asAdministrator)
        {
            return Result.Failure(Error.From(ErrorCodes.PreferenceAdministratorOnly));
        }

        if (!declaration.Admits(value))
        {
            return Result.Failure(Error.From(ErrorCodes.PreferenceWrongType));
        }

        _values.TryGetValue(name, out string? held);
        _values[name] = value;

        if (Size > maximumSize)
        {
            if (held is null)
            {
                _values.Remove(name);
            }
            else
            {
                _values[name] = held;
            }

            return Result.Failure(Error.From(ErrorCodes.PreferenceTooLarge));
        }

        return Result.Success();
    }

    /// <summary>
    /// Gives up a value the account set, returning the key to the default the host
    /// declared.
    /// </summary>
    /// <param name="name">The key.</param>
    /// <exception cref="ArgumentNullException">The key is absent.</exception>
    public void Clear(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        _values.Remove(name);
    }
}
