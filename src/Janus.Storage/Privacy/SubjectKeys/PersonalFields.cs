using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Privacy.SubjectKeys;

/// <summary>
/// The cipher of a host's own declared fields, over the <c>subject_keys</c> table.
/// </summary>
/// <param name="context">The context of the scope, whose connection and transaction the host's work shares.</param>
/// <param name="ring">The key ring the key-encryption key is borrowed from at each unwrap.</param>
/// <param name="randomness">The randomness an initialisation vector is drawn from.</param>
/// <param name="declaration">What the host declared about its own domain.</param>
/// <remarks>
/// Implements PRIV-RIGHT-005a, CONV-DESIGN-003 and CONV-CODE-007 (D-203). The keys of a
/// page are read in one statement and each is unwrapped once, before the use is called;
/// every key is cleared when the use returns, whatever it answered or threw.
/// </remarks>
internal sealed class PersonalFields(
    StoreContext context,
    IKeyRing ring,
    RandomNumberGenerator randomness,
    AuthorizationDeclaration declaration) : IPersonalFields
{
    private readonly HashSet<(ResourceType Type, string Field)> _declared =
    [
        .. declaration.ResourceTypes.SelectMany(type => type.EncryptedFields.Select(field => (type.Name, field.Name))),
    ];

    /// <inheritdoc/>
    public async ValueTask<Result<TValue>> WithSubjectAsync<TValue>(
        SubjectId subject,
        Func<SubjectFields, Result<TValue>> use,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(use);

        return await WithSubjectsAsync([subject], lent => use(lent[subject]), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<TValue>> WithSubjectsAsync<TValue>(
        IReadOnlyList<SubjectId> subjects,
        Func<IReadOnlyDictionary<SubjectId, SubjectFields>, Result<TValue>> use,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subjects);
        ArgumentNullException.ThrowIfNull(use);

        var asked = subjects
            .Distinct()
            .ToDictionary(SubjectKeyId.Of);
        SubjectKeyId[] rows = [.. asked.Keys];

        List<SubjectKeyRecord> held = await context.SubjectKeys
            .Where(key => rows.Contains(key.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // The page is judged whole: a subject the deployment holds no key for is none the
        // host can have written a field for, and nothing of the page is lent.
        if (held.Count != rows.Length)
        {
            return Result.Failure<TValue>(Error.From(
                ErrorCodes.RequestInvalid,
                "member",
                JsonSerializer.SerializeToElement("subject")));
        }

        var lent = new Dictionary<SubjectId, SubjectFields>(held.Count);
        var loans = new List<LentFields>(held.Count);

        try
        {
            foreach (SubjectKeyRecord key in held)
            {
                LentFields loan = Lent(asked[key.Id], key);

                loans.Add(loan);
                lent.Add(asked[key.Id], loan);
            }

            return use(lent);
        }
        finally
        {
            foreach (LentFields loan in loans)
            {
                loan.Return();
            }
        }
    }

    // PRIV-RIGHT-005a AC12: one unwrap per subject, whatever the use then reads. An erased
    // key is never unwrapped: every unwrap refuses it, and nothing is read under it.
    private LentFields Lent(SubjectId subject, SubjectKeyRecord key) =>
        new(
            subject,
            key.FormatMarker == PersonalDataFormat.ErasedMarker
                ? null
                : PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey, ring),
            _declared,
            randomness);
}
