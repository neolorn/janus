using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Policies;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Accounts;

/// <summary>
/// The image an account shows for itself: whether it shows one at all, what an upload
/// has to pass to become one, and what is stored when it does.
/// </summary>
/// <param name="directory">Where the photo is read and written.</param>
/// <param name="restriction">Whether the account's processing is restricted, as the gate answers it.</param>
/// <param name="policies">The policy in force for the account, which says whether it shows a photo.</param>
/// <param name="configuration">Where the bounds are read.</param>
/// <param name="audit">Where a change the account made to itself is recorded.</param>
/// <param name="work">The transaction the whole of one operation runs in.</param>
/// <param name="codec">
/// What the deployment reads images with, or nothing where it declared none.
/// </param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements IDN-ATTR-002, IDN-ATTR-003, IDN-ATTR-004 and LIB-HOST-001. The library
/// holds the photo and reads no image: what an upload is, and what it is worth
/// re-encoding it to, is the codec's, and the library stores what the codec answers
/// and nothing the request said about it.
/// </remarks>
internal sealed class ProfilePhotos(
    IAccountDirectory directory,
    ISettingsRestriction restriction,
    PolicyResolution policies,
    IConfigurationStore configuration,
    IAccountAudit audit,
    IUnitOfWork work,
    ImageCodec? codec,
    TimeProvider time)
{
    private static readonly AuditAction ProfileChanged = AuditActions.ProfileChanged;

    /// <summary>
    /// The refusal of a policy change that would show photos where the host declared no
    /// image codec: the library reads no image itself, so the value is refused at the
    /// change rather than stored for an upload to meet (OPS-CFG-003, X6).
    /// </summary>
    public static Error Undeclared { get; } = new(
        ErrorCodes.ConfigurationValueNotAllowed,
        new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
        {
            ["field"] = JsonSerializer.SerializeToElement("photos"),
            ["requires"] = JsonSerializer.SerializeToElement("imageCodec"),
        });

    /// <summary>
    /// The image an account shows for itself.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The stored JPEG, or the refusal: <c>identity.photo.notfound</c> where the account
    /// shows none and where the policy in force for it withholds photos, alike.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result<ReadOnlyMemory<byte>>> ReadAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Effective is SubjectId subject
            ? await ReadOfAsync(subject, cancellationToken).ConfigureAwait(false)
            : Result.Failure<ReadOnlyMemory<byte>>(Error.From(ErrorCodes.Denied));
    }

    /// <summary>
    /// The image one account shows, for whoever the caller has already decided may see
    /// it.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The stored JPEG, or the refusal: <c>identity.photo.notfound</c> where the account
    /// shows none and where the policy in force for it withholds photos, alike.
    /// </returns>
    public async ValueTask<Result<ReadOnlyMemory<byte>>> ReadOfAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        bool shown = (await ShownAsync(subject, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<bool>(error, ref failure));

        if (failure is Error refused)
        {
            return Result.Failure<ReadOnlyMemory<byte>>(refused);
        }

        // A policy that shows no photo shows nothing of one set while it did, and an
        // account that never set one answers the same way (09 section 6).
        ReadOnlyMemory<byte> image = shown
            ? await directory.PhotoAsync(subject, cancellationToken).ConfigureAwait(false)
            : ReadOnlyMemory<byte>.Empty;

        return image.IsEmpty
            ? Result.Failure<ReadOnlyMemory<byte>>(Error.From(ErrorCodes.PhotoNotFound))
            : Result.Success(image);
    }

    /// <summary>
    /// Makes an upload into the image an account shows, replacing what it showed.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="upload">The bytes the request carried.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal and its code.</returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result> SetAsync(
        AccessContext context,
        ReadOnlyMemory<byte> upload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        // IDN-ACCT-007 AC2: a restricted account changes none of its settings.
        if (await restriction.RefusedAsync(context, cancellationToken).ConfigureAwait(false)
            is Error restricted)
        {
            return Result.Failure(restricted);
        }

        Error? failure = null;

        bool shown = (await ShownAsync(subject, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<bool>(error, ref failure));

        if (failure is Error unavailable)
        {
            return Result.Failure(unavailable);
        }

        // A deployment whose policy shows photos and which declared no codec is
        // refused at startup, so arriving here without one is a key turned on since,
        // and the upload is refused rather than stored unread (LIB-HOST-001).
        if (!shown || codec is not ImageCodec images)
        {
            return Result.Failure(Error.From(ErrorCodes.PhotoNotEnabled));
        }

        int maximum = (await configuration.ReadAsync(Settings.PhotoMaxBytes, cancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, error => Withheld<int>(error, ref failure));

        int dimension = (await configuration.ReadAsync(Settings.PhotoMaxDimension, cancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, error => Withheld<int>(error, ref failure));

        if (failure is Error unreadable)
        {
            return Result.Failure(unreadable);
        }

        if (upload.Length > maximum)
        {
            return Result.Failure(Error.From(ErrorCodes.PhotoTooLarge));
        }

        ReadOnlyMemory<byte>? reencoded = await images
            .Reencode(upload, dimension, cancellationToken)
            .ConfigureAwait(false);

        if (reencoded is not ReadOnlyMemory<byte> image || image.IsEmpty)
        {
            return Result.Failure(Error.From(ErrorCodes.PhotoInvalid));
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await restriction.RefusedAsync(context, cancellationToken).ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        await directory.RecordPhotoAsync(subject, image, now, cancellationToken)
            .ConfigureAwait(false);

        await audit
            .RecordedAsync(ProfileChanged, Acting(context, subject), context.BreakGlassReason, subject, now, cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <summary>
    /// Gives up the image an account shows. It is not held to the policy: an image the
    /// account put there is the account's to take down, and a policy switched off
    /// after it was set would otherwise leave it there for good.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal and its code.</returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result> RemoveAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        // IDN-ACCT-007 AC2: a restricted account changes none of its settings.
        if (await restriction.RefusedAsync(context, cancellationToken).ConfigureAwait(false)
            is Error restricted)
        {
            return Result.Failure(restricted);
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await restriction.RefusedAsync(context, cancellationToken).ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        await directory.RemovePhotoAsync(subject, cancellationToken).ConfigureAwait(false);

        await audit
            .RecordedAsync(ProfileChanged, Acting(context, subject), context.BreakGlassReason, subject, now, cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    private static SubjectId Acting(AccessContext context, SubjectId subject) =>
        context.Acting ?? subject;

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // IDN-ATTR-002: availability is the policy field photos, resolved as AUTH-PRIN-002
    // resolves every field, so an account of no organization follows the system policy
    // and one of several shows a photo only where every one of them shows one.
    private async ValueTask<Result<bool>> ShownAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        (await policies.ForAsync(subject, cancellationToken).ConfigureAwait(false))
        .Match(policy => Result.Success(policy.Photos), Result.Failure<bool>);
}
