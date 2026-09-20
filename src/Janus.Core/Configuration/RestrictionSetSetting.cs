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
