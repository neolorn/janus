using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Consents;
using Janus.Privacy.Policies;

namespace Janus.Privacy.Documents;

/// <summary>
/// The legal documents a deployment publishes, each version binding in the one
/// language it names.
/// </summary>
/// <param name="store">Where the published versions are.</param>
/// <param name="scope">Whether the caller may publish at all.</param>
/// <param name="supersession">What a material revision does to the consents given.</param>
/// <param name="configuration">Where the default governing language is read.</param>
/// <param name="audit">Where a publication is written down.</param>
/// <param name="alerts">Where a refused publication is raised.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements LIB-API-005, PRIV-CONS-005 and PRIV-CONS-006. The version follows the
/// governing text: a translation attaches to a published version and corrects it
/// without making another, because what was shown as authoritative did not change.
/// </remarks>
internal sealed class LegalDocumentService(
    ILegalDocumentStore store,
    AdministrativeScope scope,
    Supersession supersession,
    IConfigurationStore configuration,
    IPrivacyAudit audit,
    IPrivacyAlerts alerts,
    IUnitOfWork work,
    TimeProvider time) : ILegalDocuments
{
    private static readonly AuditAction Published = AuditActions.DocumentPublished;

    private static readonly AuditAction Translated = AuditActions.DocumentTranslated;

    /// <inheritdoc/>
    public async ValueTask<Result<DocumentVersion>> ReadAsync(
        string document,
        string? version,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(document);

        DocumentVersion? found = version is { Length: > 0 } named
            ? await store.FindAsync(document, named, cancellationToken).ConfigureAwait(false)
            : await store.CurrentAsync(document, cancellationToken).ConfigureAwait(false);

        return found is null
            ? Result.Failure<DocumentVersion>(Error.From(ErrorCodes.DocumentNotFound))
            : Result.Success(found);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<DocumentVersion>> PublishAsync(
        AccessContext context,
        DocumentPublication publication,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(publication);

        if (await scope.RefusedAsync(context, Permissions.NoticePublish, cancellationToken)
                .ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure<DocumentVersion>(refused);
        }

        // A translation is an aid to the reader and never governs, so a submission
        // carrying only translations publishes nothing (PRIV-CONS-006).
        if (string.IsNullOrWhiteSpace(publication.Text))
        {
            await alerts
                .RaiseAsync(
                    AlertCondition.GoverningTextMissing,
                    publication.DocumentName,
                    Named(publication.DocumentName),
                    cancellationToken)
                .ConfigureAwait(false);

            return Result.Failure<DocumentVersion>(Error.From(
                ErrorCodes.NoticeGoverningTextMissing,
                "document",
                JsonSerializer.SerializeToElement(publication.DocumentName)));
        }

        Result<string> governing = publication.GoverningLanguage is { Length: > 0 } declared
            ? Result.Success(declared)
            : await configuration
                .ReadAsync(Settings.LegalGoverningLanguage, cancellationToken)
                .ConfigureAwait(false);

        return await governing
            .Match(
                language => WriteAsync(context, publication, language, cancellationToken),
                error => ValueTask.FromResult(Result.Failure<DocumentVersion>(error)))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> TranslateAsync(
        AccessContext context,
        string document,
        string version,
        DocumentTranslation translation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(translation);

        if (await scope.RefusedAsync(context, Permissions.NoticePublish, cancellationToken)
                .ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure(refused);
        }

        if (await store.FindAsync(document, version, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure(Error.From(ErrorCodes.DocumentNotFound));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await store.TranslateAsync(document, version, translation, cancellationToken).ConfigureAwait(false);
        await audit
            .RecordedAsync(
                Translated,
                context.Acting,
                subject: null,
                time.GetUtcNow(),
                Named(document, version, translation.Language),
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static Dictionary<string, JsonElement> Named(string document) =>
        new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
        {
            ["document"] = JsonSerializer.SerializeToElement(document),
        };

    private static Dictionary<string, JsonElement> Named(
        string document,
        string version,
        string language) =>
        new Dictionary<string, JsonElement>(capacity: 3, StringComparer.Ordinal)
        {
            ["document"] = JsonSerializer.SerializeToElement(document),
            ["version"] = JsonSerializer.SerializeToElement(version),
            ["language"] = JsonSerializer.SerializeToElement(language),
        };

    private static Dictionary<string, JsonElement> Named(
        DocumentVersion version,
        bool material,
        int superseded) =>
        new Dictionary<string, JsonElement>(capacity: 6, StringComparer.Ordinal)
        {
            ["document"] = JsonSerializer.SerializeToElement(version.DocumentName),
            ["version"] = JsonSerializer.SerializeToElement(version.Version),
            ["governingLanguage"] = JsonSerializer.SerializeToElement(version.GoverningLanguage),
            ["material"] = JsonSerializer.SerializeToElement(material),
            ["superseded"] = JsonSerializer.SerializeToElement(superseded),
            ["translations"] = JsonSerializer.SerializeToElement(
                version.Translations.Select(translation => translation.Language).ToArray()),
        };

    private async ValueTask<Result<DocumentVersion>> WriteAsync(
        AccessContext context,
        DocumentPublication publication,
        string governing,
        CancellationToken cancellationToken)
    {
        int published = await store
            .CountAsync(publication.DocumentName, cancellationToken)
            .ConfigureAwait(false);

        var version = new DocumentVersion(
            publication.DocumentName,
            (published + 1).ToString(CultureInfo.InvariantCulture),
            governing,
            publication.Text,
            [.. publication.Translations],
            time.GetUtcNow());

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await store.AddAsync(version, cancellationToken).ConfigureAwait(false);

        // PRIV-CONS-007: only the notice is what a consent record names the version
        // of, so a material revision of anything else ends no consent.
        Result<int> ended =
            publication.Material && string.Equals(version.DocumentName, ConsentService.Notice, StringComparison.Ordinal)
                ? await supersession
                    .OfAsync(version.Version, version.PublishedAt, cancellationToken)
                    .ConfigureAwait(false)
                : Result.Success(0);

        if (ended.Match(_ => (Error?)null, error => error) is Error unended)
        {
            return Result.Failure<DocumentVersion>(unended);
        }

        int superseded = ended.Match(count => count, _ => 0);

        await audit
            .RecordedAsync(
                Published,
                context.Acting,
                subject: null,
                version.PublishedAt,
                Named(version, publication.Material, superseded),
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(version);
    }
}
