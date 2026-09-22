using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Janus.Core.Configuration;

/// <summary>
/// The named restriction set governing every send: the one key of chapter 10 section
/// 4.5 whose value is a list of restrictions.
/// </summary>
/// <remarks>Implements chapter 10 sections 4.5 and 5.14 to 5.16, AUTH-ABUSE-004.</remarks>
public sealed class RestrictionSetSetting : Setting<IReadOnlyList<Restriction>>
{
    private const string HostPrefix = "host:";

    internal RestrictionSetSetting(
        string key,
        SettingScope scope,
        IReadOnlyList<Restriction> fallback)
        : base(key, scope, SettingDirection.AnyChange, required: false, fallback)
    {
    }

    /// <summary>
    /// Whether replacing one restriction with another lets more through than before.
    /// </summary>
    /// <param name="before">What stood, or nothing where the restriction is new.</param>
    /// <param name="after">What replaces it, or nothing where it is deleted.</param>
    /// <returns>Whether the change is a loosening.</returns>
    /// <remarks>
    /// Implements AUTH-ABUSE-004 and OPS-CFG-002. A deleted restriction, a widened key
    /// or purpose, a higher maximum, a shorter interval and a dropped bucket all let
    /// more through; a restriction that is new lets through nothing that was not
    /// already getting through.
    /// </remarks>
    public static bool Loosens(Restriction? before, Restriction? after)
    {
        if (before is null)
        {
            return false;
        }

        if (after is null)
        {
            return true;
        }

        if (after.Key != before.Key
            || after.HostKeyName != before.HostKeyName
            || (after.Purpose is not RestrictionPurpose.Any && after.Purpose != before.Purpose))
        {
            return true;
        }

        return before.Buckets.Any(bucket => !after.Buckets.Any(kept => AtLeastAsStrict(kept, bucket)));
    }

    /// <inheritdoc />
    /// <remarks>
    /// The restriction set is the one key whose own chapter states its direction, which
    /// chapter 10 section 4 lets govern: a set loosens where any restriction in it was
    /// deleted or replaced by one that lets more through, and a set that only gains
    /// restrictions or tightens them does not (AUTH-ABUSE-004, OPS-CFG-002).
    /// </remarks>
    /// <exception cref="ArgumentNullException">Either set is absent.</exception>
    public override bool Loosens(IReadOnlyList<Restriction> before, IReadOnlyList<Restriction> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        return before.Any(one => Loosens(
            one,
            after.FirstOrDefault(kept => string.Equals(kept.Name, one.Name, StringComparison.Ordinal))));
    }

    /// <inheritdoc />
    public override Result<IReadOnlyList<Restriction>> Accept(IReadOnlyList<Restriction> value) =>
        value is null || value.Any(restriction => restriction is null || restriction.Buckets.Count == 0)
            ? Result.Failure<IReadOnlyList<Restriction>>(
                Refused(ErrorCodes.ConfigurationValueNotAllowed, "allowed", "restrictions, each with at least one bucket"))
            : Result.Success(value);

    /// <inheritdoc />
    private protected override Result<IReadOnlyList<Restriction>> Parse(string stored) =>
        SettingText
            .Shape<Written[]>(stored, Malformed())
            .Match(Read, Result.Failure<IReadOnlyList<Restriction>>);

    /// <inheritdoc />
    private protected override string Render(IReadOnlyList<Restriction> value) =>
        SettingText.OfShape(value.Select(Write).ToArray());

    private static bool AtLeastAsStrict(Bucket kept, Bucket bucket) =>
        kept.Maximum <= bucket.Maximum
        && kept.Interval >= bucket.Interval
        && (kept.Window == bucket.Window || kept.Window is BucketWindow.Sliding);

    private static Written Write(Restriction restriction) =>
        new(
            restriction.Name,
            restriction.Key is RestrictionKeyKind.Host
                ? HostPrefix + restriction.HostKeyName
                : SettingText.Of(restriction.Key),
            SettingText.Of(restriction.Purpose),
            [.. restriction.Buckets.Select(
                bucket => new WrittenBucket(
                    bucket.Maximum,
                    XmlConvert.ToString(bucket.Interval),
                    SettingText.Of(bucket.Window)))]);

    private Result<IReadOnlyList<Restriction>> Read(Written[] written)
    {
        var read = new List<Restriction>(written.Length);

        foreach (Written restriction in written)
        {
            if (restriction is null
                || restriction.Name is null
                || restriction.Purpose is null
                || restriction.Buckets is null
                || !KeyKind(restriction.Key, out RestrictionKeyKind kind, out string? hostKeyName)
                || !SettingText.TryRead(restriction.Purpose, out RestrictionPurpose purpose))
            {
                return Result.Failure<IReadOnlyList<Restriction>>(Malformed());
            }

            var buckets = new List<Bucket>(restriction.Buckets.Length);

            foreach (WrittenBucket bucket in restriction.Buckets)
            {
                if (bucket is null
                    || bucket.Window is null
                    || bucket.Interval is null
                    || !Duration.TryParse(bucket.Interval, out TimeSpan interval)
                    || !SettingText.TryRead(bucket.Window, out BucketWindow window))
                {
                    return Result.Failure<IReadOnlyList<Restriction>>(Malformed());
                }

                buckets.Add(new Bucket(bucket.Max, interval, window));
            }

            read.Add(new Restriction(restriction.Name, kind, hostKeyName, purpose, buckets));
        }

        return Result.Success<IReadOnlyList<Restriction>>(read);
    }

    private static bool KeyKind(string? written, out RestrictionKeyKind kind, out string? hostKeyName)
    {
        hostKeyName = null;

        if (written is null)
        {
            kind = default;
            return false;
        }

        if (written.StartsWith(HostPrefix, StringComparison.Ordinal))
        {
            kind = RestrictionKeyKind.Host;
            hostKeyName = written[HostPrefix.Length..];
            return hostKeyName.Length > 0;
        }

        return SettingText.TryRead(written, out kind);
    }

    private Error Malformed() =>
        Refused(ErrorCodes.ConfigurationValueNotAllowed, "allowed", "restrictions, each with at least one bucket");

    // The shape chapter 09 gives the restriction endpoints, which is what the settings
    // table holds and what the management application reads back.
    private sealed record Written(string? Name, string? Key, string? Purpose, WrittenBucket[]? Buckets);

    private sealed record WrittenBucket(int Max, string? Interval, string? Window);
}
