using System;

namespace Janus.Core.Configuration;

/// <summary>
/// How a family's value is written in the settings table and read back from it. A
/// family has one key per organization or per host-declared category, so the form is
/// stated once for the family rather than once per member.
/// </summary>
/// <typeparam name="TValue">The type of a member's value.</typeparam>
/// <param name="Parse">Reads the value, or the failure the caller supplies.</param>
/// <param name="Render">Writes the value as the settings table holds it.</param>
/// <param name="Expected">The form the key writes, as the chapter writes it.</param>
/// <remarks>Implements OPS-CFG-008 and chapter 10 section 4 value types.</remarks>
internal sealed record SettingForm<TValue>(
    Func<string, Error, Result<TValue>> Parse,
    Func<TValue, string> Render,
    string Expected);
