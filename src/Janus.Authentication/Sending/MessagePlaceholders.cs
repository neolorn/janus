using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Sending;

/// <summary>
/// The places a template leaves for the library's values, and how wide each of them
/// can get. A template is measured against its budget with every place at its widest,
/// so a message that fits one text message at startup fits one at every send.
/// </summary>
/// <remarks>
/// Implements INT-SMS-003, INT-SMS-005a and AUTH-ABUSE-005. The width is defined once
/// per place, here, beside the messages themselves. A value whose width the library
/// fixes carries that width; a name a deployment or a host chooses is held to the rule
/// of its place where it is written or declared, and carries the width that rule
/// bounds. The subscribers still to confirm an erasure, the categories a key is named
/// after and the origins a link lands on are the deployment's, so those three widths are
/// measured at startup from what it registered and declared.
/// </remarks>
internal sealed class MessagePlaceholders
{
    // A letter of the default alphabet, so filling a place leaves the template in the
    // alphabet it was written in and the budget it is measured against unchanged.
    private const char Filler = 'W';

    // What the library's own values are as wide as.
    private static readonly int Instant =
        DateTimeOffset.UnixEpoch.ToString("O", CultureInfo.InvariantCulture).Length;

    private static readonly int Count = int.MinValue.ToString(CultureInfo.InvariantCulture).Length;

    private static readonly int Amount =
        decimal.MinValue.ToString(CultureInfo.InvariantCulture).Length;

    private static readonly int Identifier = Guid.Empty.ToString("D", CultureInfo.InvariantCulture).Length;

    private static readonly int Condition = Widest(Enum.GetValues<AlertCondition>().Select(WrittenName.Of));

    private static readonly int RequestType = Widest(Enum.GetValues<PrivacyRequestType>().Select(WrittenName.Of));

    private static readonly int RequestStatus = Widest(Enum.GetValues<PrivacyRequestStatus>().Select(WrittenName.Of));

    // What a name a deployment or a host chooses is measured at: a restriction's and a
    // governing document's are held where they are written or declared to the rule of
    // INT-SMS-003, which bounds them at this width.
    private const int Name = 64;

    // The kind of a subject event, which the privacy area names and this one cannot
    // read, measured at the width of the longest kind that area may ever carry.
    private const int EventKind = 32;

    private readonly FrozenDictionary<string, int> _widths;

    /// <summary>
    /// The places measured for one deployment.
    /// </summary>
    /// <param name="required">
    /// The names of the registered subject-event subscribers an erasure waits for.
    /// </param>
    /// <param name="categories">
    /// The data categories the host declared a retention floor for, each of which names
    /// a key of its own.
    /// </param>
    /// <param name="landing">The origins a link lands on.</param>
    /// <exception cref="ArgumentNullException">One is absent.</exception>
    public MessagePlaceholders(
        IEnumerable<string> required,
        IEnumerable<string> categories,
        LandingOrigins landing)
    {
        ArgumentNullException.ThrowIfNull(required);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(landing);

        _widths = FrozenDictionary.ToFrozenDictionary<string, int>(
        [
            new KeyValuePair<string, int>("code", Factors.VerificationCode.Digits),
            new KeyValuePair<string, int>("link", LandingLinks.Widest(landing)),
            new KeyValuePair<string, int>("condition", Condition),
            new KeyValuePair<string, int>("raisedAt", Instant),
            new KeyValuePair<string, int>("restriction", Name),
            new KeyValuePair<string, int>("key", SettingKey(categories)),
            new KeyValuePair<string, int>("destinationsBefore", Count),
            new KeyValuePair<string, int>("destinationsAfter", Count),
            new KeyValuePair<string, int>("balance", Amount),
            new KeyValuePair<string, int>("floor", Amount),
            new KeyValuePair<string, int>("spentLastHour", Amount),
            new KeyValuePair<string, int>("document", Name),
            new KeyValuePair<string, int>("delivery", Identifier),
            new KeyValuePair<string, int>("kind", EventKind),
            new KeyValuePair<string, int>("attempts", Count),
            new KeyValuePair<string, int>("outstanding", Joined(required)),
            new KeyValuePair<string, int>("request", Identifier),
            new KeyValuePair<string, int>("type", RequestType),
            new KeyValuePair<string, int>("status", RequestStatus),
            new KeyValuePair<string, int>("decisionDue", Instant),
        ],
        StringComparer.Ordinal);
    }

    /// <summary>
    /// Every place the library fills, with the width it is measured at.
    /// </summary>
    public IReadOnlyDictionary<string, int> Widths => _widths;

    /// <summary>
    /// Whether a template carries a link, which a text is budgeted two segments for.
    /// </summary>
    /// <param name="text">The template.</param>
    /// <returns>Whether it names the place <c>link</c>.</returns>
    /// <exception cref="ArgumentNullException">The template is absent.</exception>
    public static bool CarriesLink(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.Contains("{link}", StringComparison.Ordinal);
    }

    /// <summary>
    /// The first place a template names that the library does not fill, which has no
    /// width to be measured at. A place is a name of letters and digits in braces.
    /// </summary>
    /// <param name="text">The template.</param>
    /// <returns>The place, or nothing where every place it names is filled.</returns>
    /// <exception cref="ArgumentNullException">The template is absent.</exception>
    public string? Unlisted(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        int opened = text.IndexOf('{', StringComparison.Ordinal);

        while (opened >= 0)
        {
            int closed = text.IndexOf('}', opened + 1);

            if (closed < 0)
            {
                return null;
            }

            string named = text[(opened + 1)..closed];

            if (named.Length > 0 && named.All(char.IsAsciiLetterOrDigit) && !_widths.ContainsKey(named))
            {
                return named;
            }

            opened = text.IndexOf('{', opened + 1);
        }

        return null;
    }

    /// <summary>
    /// One template with every place it names at its widest.
    /// </summary>
    /// <param name="text">The template.</param>
    /// <returns>The widest the template renders to.</returns>
    /// <exception cref="ArgumentNullException">The template is absent.</exception>
    public string Widest(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var filled = new StringBuilder(text);

        foreach (KeyValuePair<string, int> place in _widths)
        {
            _ = filled.Replace("{" + place.Key + "}", new string(Filler, place.Value));
        }

        return filled.ToString();
    }

    // The widest key a change can name: every key of the catalogue, and each family at
    // its widest parameter, an organization's identifier or the longest category.
    private static int SettingKey(IEnumerable<string> categories)
    {
        int category = Widest(categories);
        int widest = Widest(Settings.All.Select(setting => setting.Key.ToString()));

        foreach (SettingFamily family in Settings.Families)
        {
            int parameter = ReferenceEquals(family, Settings.HostCategoryRetention) ? category : Identifier;

            if (parameter is not 0)
            {
                widest = Math.Max(widest, family.Prefix.Length + 1 + parameter);
            }
        }

        return widest;
    }

    // The subscribers still to confirm are carried as the list the alert details hold
    // and rendered as the alert channels render any detail, so the widest is every
    // required subscriber outstanding at once, written the same way.
    private static int Joined(IEnumerable<string> required) =>
        JsonSerializer.SerializeToElement(required.ToArray()).ToString().Length;

    private static int Widest(IEnumerable<string> written)
    {
        int widest = 0;

        foreach (string one in written)
        {
            widest = Math.Max(widest, one.Length);
        }

        return widest;
    }
}
