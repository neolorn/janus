using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Privacy.Consents;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Privacy.Consents;

/// <summary>
/// The consent and objection records, over the <c>consents</c> and
/// <c>objections</c> tables.
/// </summary>
/// <param name="context">The context the operation's reads run on.</param>
/// <param name="connections">Where each written statement takes its connection from.</param>
/// <remarks>
/// Implements PRIV-CONS-001, PRIV-RIGHT-001a and CONV-DESIGN-003. Nothing here
/// deletes or overwrites: a grant is a row of its own, and a withdrawal or a
/// supersession writes a timestamp onto the row that is there, because the record is
/// the evidence the law asks for. Each write is one conditional statement, so what it
/// answers is what the table then holds, and no read keeps a row the context would
/// answer from afterwards.
/// </remarks>
internal sealed class ConsentStore(StoreContext context, DataConnections connections) : IConsentStore
{
    // D-166 X3: a withdrawal is decided on the record it reads, and a first grant has
    // no row to lock, so the subject's records are held as one for the rest of the
    // transaction; no read takes this lock.
    private const string Hold =
        "SELECT pg_advisory_xact_lock(hashtextextended('identity.consents/' || CAST(@subject AS text), 0));";

    // PRIV-CONS-001 AC4, AC6: the index over live rows decides whether the grant is a
    // record, so one written meanwhile, by whatever writer, leaves this one unwritten.
    private const string AddConsent =
        """
        INSERT INTO identity.consents
            (id, subject, purpose, document, notice_version, mechanism, kind, granted_at)
        VALUES
            (@id, @subject, @purpose, @document, @version, @mechanism, @kind, @at)
        ON CONFLICT (subject, purpose) WHERE withdrawn_at IS NULL AND superseded_at IS NULL DO NOTHING;
        """;

    private const string AddObjection =
        """
        INSERT INTO identity.objections
            (id, subject, purpose, document, notice_version, mechanism, recorded_at)
        VALUES
            (@id, @subject, @purpose, @document, @version, @mechanism, @at)
        ON CONFLICT (subject, purpose) WHERE withdrawn_at IS NULL DO NOTHING;
        """;

    // The record that stands for a purpose is the live one, or the latest where none
    // is live; it is stamped only where it has not been taken back.
    private const string WithdrawConsent =
        """
        UPDATE identity.consents SET withdrawn_at = @at
        WHERE id = (
                SELECT id FROM identity.consents
                WHERE subject = @subject AND purpose = @purpose
                ORDER BY (withdrawn_at IS NULL AND superseded_at IS NULL) DESC, granted_at DESC, id DESC
                LIMIT 1)
            AND withdrawn_at IS NULL;
        """;

    private const string Supersede =
        """
        UPDATE identity.consents SET superseded_at = @at
        WHERE subject = @subject AND purpose = @purpose
            AND withdrawn_at IS NULL AND superseded_at IS NULL;
        """;

    // PRIV-CONS-007 AC5: one conditional statement over every purpose at once. A row
    // another start stamps first is held until that start commits and then fails the
    // condition, so it is stamped once and answered to one of the two.
    private const string SupersedeAgainstAnother =
        """
        UPDATE identity.consents AS held SET superseded_at = @at
        FROM unnest(@purposes, @documents) AS named(purpose, document)
        WHERE held.purpose = named.purpose
            AND held.document <> named.document
            AND held.withdrawn_at IS NULL AND held.superseded_at IS NULL
        RETURNING held.subject, held.purpose;
        """;

    private const string WithdrawObjection =
        """
        UPDATE identity.objections SET withdrawn_at = @at
        WHERE subject = @subject AND purpose = @purpose AND withdrawn_at IS NULL;
        """;

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask HoldAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A subject's consents are held only inside the operation's transaction.");
        }

        _ = await ExecuteAsync(Hold, new { subject = subject.Value }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ConsentRecord>> ConsentsAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
    [
        .. (await context.Consents
                .AsNoTracking()
                .Where(consent => consent.Subject == subject)
                .OrderBy(consent => consent.GrantedAt)
                .ThenBy(consent => consent.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(Read),
    ];

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ObjectionRecord>> ObjectionsAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
    [
        .. (await context.Objections
                .AsNoTracking()
                .Where(objection => objection.Subject == subject)
                .OrderBy(objection => objection.RecordedAt)
                .ThenBy(objection => objection.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(Read),
    ];

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The record is not live.</exception>
    public async ValueTask<bool> AddAsync(
        SubjectId subject,
        ConsentRecord consent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consent);

        if (!consent.Live)
        {
            throw new ArgumentException("A consent is added live; what ends it is stamped onto it.", nameof(consent));
        }

        return await AddedAsync(
                AddConsent,
                new
                {
                    id = Guid.CreateVersion7(consent.GrantedAt),
                    subject = subject.Value,
                    purpose = consent.Purpose,
                    document = consent.Document,
                    version = consent.NoticeVersion,
                    mechanism = VocabularyConverter<ConsentMechanism>.Write(consent.Mechanism),
                    kind = VocabularyConverter<ConsentKind>.Write(consent.Kind),
                    at = consent.GrantedAt,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The record is not standing.</exception>
    public async ValueTask<bool> AddAsync(
        SubjectId subject,
        ObjectionRecord objection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objection);

        if (!objection.Standing)
        {
            throw new ArgumentException("An objection is added standing; its withdrawal is stamped onto it.", nameof(objection));
        }

        return await AddedAsync(
                AddObjection,
                new
                {
                    id = Guid.CreateVersion7(objection.RecordedAt),
                    subject = subject.Value,
                    purpose = objection.Purpose,
                    document = objection.Document,
                    version = objection.NoticeVersion,
                    mechanism = VocabularyConverter<ConsentMechanism>.Write(objection.Mechanism),
                    at = objection.RecordedAt,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> WithdrawConsentAsync(
        SubjectId subject,
        string purpose,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(WithdrawConsent, new { subject = subject.Value, purpose, at }, cancellationToken)
            .ConfigureAwait(false) == 1;

    /// <inheritdoc/>
    public async ValueTask<bool> SupersedeAsync(
        SubjectId subject,
        string purpose,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(Supersede, new { subject = subject.Value, purpose, at }, cancellationToken)
            .ConfigureAwait(false) == 1;

    /// <inheritdoc/>
    public async ValueTask<bool> WithdrawObjectionAsync(
        SubjectId subject,
        string purpose,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(WithdrawObjection, new { subject = subject.Value, purpose, at }, cancellationToken)
            .ConfigureAwait(false) == 1;

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<HeldConsent>> LiveAgainstAnotherAsync(
        IReadOnlyCollection<string> purposes,
        string document,
        string version,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(purposes);

        string[] named = [.. purposes];

        return
        [
            .. (await context.Consents
                    .AsNoTracking()
                    .Where(consent => consent.WithdrawnAt == null
                        && consent.SupersededAt == null
                        && named.Contains(consent.Purpose)
                        && (consent.Document != document || consent.NoticeVersion != version))
                    .OrderBy(consent => consent.GrantedAt)
                    .ThenBy(consent => consent.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false))
                .Select(consent => new HeldConsent(consent.Subject, Read(consent))),
        ];
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<EndedConsent>> SupersedeAgainstAnotherAsync(
        IReadOnlyDictionary<string, string> documents,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documents);

        string[] purposes = [.. documents.Keys];
        string[] named = [.. purposes.Select(purpose => documents[purpose])];

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        return
        [
            .. (await ambient.Connection
                    .QueryAsync<(Guid Subject, string Purpose)>(new CommandDefinition(
                        SupersedeAgainstAnother,
                        new { purposes, documents = named, at },
                        ambient.Transaction,
                        cancellationToken: cancellationToken))
                    .ConfigureAwait(false))
                .Select(ended => new EndedConsent(new SubjectId(ended.Subject), ended.Purpose)),
        ];
    }

    private static ConsentRecord Read(ConsentRecordRow row) =>
        new(
            row.Purpose,
            row.Document,
            row.NoticeVersion,
            row.Mechanism,
            row.Kind,
            row.GrantedAt,
            row.WithdrawnAt,
            row.SupersededAt);

    private static ObjectionRecord Read(ObjectionRecordRow row) =>
        new(row.Purpose, row.Document, row.NoticeVersion, row.Mechanism, row.RecordedAt, row.WithdrawnAt);

    // A record names its subject's account, which the same operation may have added
    // and not yet saved (a registration's consents), so what the context tracks is
    // written before the statement that refers to it.
    private async ValueTask<bool> AddedAsync(
        string statement,
        object parameters,
        CancellationToken cancellationToken)
    {
        _ = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await ExecuteAsync(statement, parameters, cancellationToken).ConfigureAwait(false) == 1;
    }

    private async ValueTask<int> ExecuteAsync(
        string statement,
        object parameters,
        CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        return await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                statement,
                parameters,
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
