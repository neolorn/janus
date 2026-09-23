using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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
/// fixes carries that width; one a deployment or a host chooses carries the widest
/// the library will measure against, and a wider one costs a second message rather
/// than a refusal at the moment somebody is waiting.
/// </remarks>
internal static class MessagePlaceholders
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

    private static readonly int SettingKey = Widest(Settings.All.Select(setting => setting.Key.ToString()));

    private static readonly int RequestType = Widest(Enum.GetNames<PrivacyRequestType>());

    private static readonly int RequestStatus = Widest(Enum.GetNames<PrivacyRequestStatus>());

    // What a name a deployment or a host chooses is measured at. A restriction name,
    // a governing document and the subscribers still to confirm an erasure are all
    // outside the library's gift, so each carries the width past which the library
    // refuses to promise one message.
    private const int Name = 64;

    private const int Names = 128;

    // The kind of a subject event, which the privacy area names and this one cannot
    // read, measured at the width of the longest kind that area may ever carry.
    private const int EventKind = 32;

    private static readonly FrozenDictionary<string, int> Defined =
        FrozenDictionary.ToFrozenDictionary<string, int>(
        [
            new KeyValuePair<string, int>("code", Factors.VerificationCode.Digits),
            new KeyValuePair<string, int>("token", OpaqueToken.Width),
            new KeyValuePair<string, int>("condition", Condition),
            new KeyValuePair<string, int>("raisedAt", Instant),
            new KeyValuePair<string, int>("restriction", Name),
            new KeyValuePair<string, int>("key", SettingKey),
            new KeyValuePair<string, int>("destinationsBefore", Count),
            new KeyValuePair<string, int>("destinationsAfter", Count),
            new KeyValuePair<string, int>("balance", Amount),
            new KeyValuePair<string, int>("floor", Amount),
            new KeyValuePair<string, int>("spentLastHour", Amount),
            new KeyValuePair<string, int>("document", Name),
            new KeyValuePair<string, int>("delivery", Identifier),
            new KeyValuePair<string, int>("kind", EventKind),
            new KeyValuePair<string, int>("attempts", Count),
            new KeyValuePair<string, int>("outstanding", Names),
            new KeyValuePair<string, int>("request", Identifier),
            new KeyValuePair<string, int>("type", RequestType),
            new KeyValuePair<string, int>("status", RequestStatus),
            new KeyValuePair<string, int>("decisionDue", Instant),
        ],
        StringComparer.Ordinal);

    /// <summary>
    /// Every place the library fills, with the width it is measured at.
    /// </summary>
    public static IReadOnlyDictionary<string, int> Widths => Defined;

    /// <summary>
    /// One template with every place it names at its widest. A place the library does
    /// not fill is left as it stands, which is what a send does with it too.
    /// </summary>
    /// <param name="text">The template.</param>
    /// <returns>The widest the template renders to.</returns>
    /// <exception cref="ArgumentNullException">The template is absent.</exception>
    public static string Widest(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var filled = new StringBuilder(text);

        foreach (KeyValuePair<string, int> place in Defined)
        {
            _ = filled.Replace("{" + place.Key + "}", new string(Filler, place.Value));
        }

        return filled.ToString();
    }

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
