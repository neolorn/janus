using System;
using System.Collections.Generic;
using System.Text;

namespace Janus.Authentication.Sending;

/// <summary>
/// Putting the library's values into the places a template leaves for them. The
/// words around them are the deployment's and are never touched.
/// </summary>
/// <remarks>
/// Implements CONV-CONTENT-001 and AUTH-ABUSE-005. A place the values do not name is
/// left as it stands, so a template is never silently emptied of its meaning.
/// </remarks>
internal static class MessageRendering
{
    /// <summary>
    /// Fills one template's places.
    /// </summary>
    /// <param name="text">The template.</param>
    /// <param name="values">What the library supplies, by name.</param>
    /// <returns>The text as it is sent.</returns>
    /// <exception cref="ArgumentNullException">Either is absent.</exception>
    public static string Fill(string text, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count == 0)
        {
            return text;
        }

        var filled = new StringBuilder(text);

        foreach (KeyValuePair<string, string> value in values)
        {
            filled.Replace("{" + value.Key + "}", value.Value);
        }

        return filled.ToString();
    }
}
