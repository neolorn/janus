using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Tests.Invitations;

/// <summary>
/// The legal documents a deployment published, held in memory, the version of each
/// published last being its current one.
/// </summary>
internal sealed class LegalDocumentsInMemory : ILegalDocuments
{
    private readonly List<DocumentVersion> _versions = [];

    /// <summary>
    /// Publishes a version of a document.
    /// </summary>
    /// <param name="document">Which document.</param>
    /// <param name="version">The version.</param>
    /// <param name="at">When it was published.</param>
    public void Publish(string document, string version, DateTimeOffset at) =>
        _versions.Add(new DocumentVersion(document, version, "en", "Text.", [], at));

    /// <inheritdoc/>
    public ValueTask<Result<DocumentVersion>> ReadAsync(
        string document,
        string? version,
        CancellationToken cancellationToken)
    {
        DocumentVersion? found = _versions
            .LastOrDefault(held => held.DocumentName == document && (version is null || held.Version == version));

        return ValueTask.FromResult(
            found is null
                ? Result.Failure<DocumentVersion>(Error.From(ErrorCodes.DocumentNotFound))
                : Result.Success(found));
    }

    /// <inheritdoc/>
    public ValueTask<Result<DocumentVersion>> PublishAsync(
        AccessContext context,
        DocumentPublication publication,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publication);

        var published = new DocumentVersion(
            publication.DocumentName,
            (_versions.Count(held => held.DocumentName == publication.DocumentName) + 1)
                .ToString(System.Globalization.CultureInfo.InvariantCulture),
            publication.GoverningLanguage ?? "en",
            publication.Text,
            publication.Translations,
            DateTimeOffset.UnixEpoch.AddSeconds(_versions.Count));

        _versions.Add(published);

        return ValueTask.FromResult(Result.Success(published));
    }

    /// <inheritdoc/>
    public ValueTask<Result> TranslateAsync(
        AccessContext context,
        string document,
        string version,
        DocumentTranslation translation,
        CancellationToken cancellationToken)
    {
        int index = _versions.FindIndex(held => held.DocumentName == document && held.Version == version);

        if (index < 0)
        {
            return ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.DocumentNotFound)));
        }

        _versions[index] = _versions[index] with { Translations = [.. _versions[index].Translations, translation] };

        return ValueTask.FromResult(Result.Success());
    }
}
