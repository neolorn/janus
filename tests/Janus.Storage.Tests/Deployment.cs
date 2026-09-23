using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Organizations;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// The key material a deployment holds and the rows a subject cannot be written
/// without: the account and its wrapped data key.
/// </summary>
/// <param name="database">The database the subjects are written to.</param>
/// <remarks>
/// One of these belongs to one test, which is one class instance, so what a test writes
/// is unreadable to every other test.
/// </remarks>
internal sealed class Deployment(DatabaseFixture database) : IDisposable
{
    /// <summary>
    /// The key the searchable fingerprints are computed under.
    /// </summary>
    public static readonly byte[] FingerprintKey =
        Encoding.UTF8.GetBytes("the fingerprint key of this deployment");

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly Dictionary<int, ReadOnlyMemory<byte>> _versions = [];

    /// <summary>
    /// The randomness the deployment runs on.
    /// </summary>
    public RandomNumberGenerator Randomness => _randomness;

    /// <summary>
    /// The versions a subject key may be wrapped under, by their number.
    /// </summary>
    public IReadOnlyDictionary<int, ReadOnlyMemory<byte>> Versions
    {
        get
        {
            if (_versions.Count == 0)
            {
                byte[] material = new byte[PersonalDataFormat.DataKeyLength];
                _randomness.GetBytes(material);
                _versions[1] = material;
            }

            return _versions;
        }
    }

    /// <summary>
    /// The key-encryption key of the deployment, at its one version.
    /// </summary>
    public KeyEncryptionKeys Keys => new(1, Versions);

    /// <summary>
    /// Writes an account and the wrapped data key its personal fields are held under.
    /// </summary>
    /// <param name="at">The instant the account was created.</param>
    /// <returns>The subject the account was issued.</returns>
    public async Task<SubjectId> AccountAsync(DateTimeOffset at)
    {
        SubjectId subject = Subjects.New();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(_randomness);

        try
        {
            await using StoreContext context = database.Context();

            context.Accounts.Add(new AccountRecord
            {
                Subject = subject,
                CreatedAt = at,
                State = AccountState.Active,
            });

            context.SubjectKeys.Add(new SubjectKeyRecord
            {
                Subject = subject,
                FormatMarker = PersonalDataFormat.Marker,
                KeyVersion = Keys.CurrentVersion,
                WrappedKey = PersonalFieldCipher.Wrap(dataKey, Keys.Current.Span),
            });

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }

        return subject;
    }

    /// <summary>
    /// Writes an organization for the rows that belong to one.
    /// </summary>
    /// <param name="at">The instant it was created.</param>
    /// <returns>The organization.</returns>
    public async Task<OrganizationId> OrganizationAsync(DateTimeOffset at)
    {
        var organization = new OrganizationId(Guid.NewGuid());

        await using StoreContext context = database.Context();

        context.Organizations.Add(new OrganizationRecord
        {
            Id = organization,
            Name = "Organization " + organization.Value.ToString("n", CultureInfo.InvariantCulture),
            CreatedAt = at,
        });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return organization;
    }

    /// <summary>
    /// Destroys a subject's data key, as the erasure transaction does.
    /// </summary>
    /// <param name="subject">Whose key to destroy.</param>
    /// <returns>The work of destroying it.</returns>
    public async Task EraseAsync(SubjectId subject)
    {
        await using StoreContext context = database.Context();

        SubjectKeyRecord key = await context.SubjectKeys
            .SingleAsync(held => held.Subject == subject, TestContext.Current.CancellationToken);

        key.FormatMarker = PersonalDataFormat.ErasedMarker;
        key.WrappedKey = new byte[PersonalDataFormat.DataKeyLength];

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose() => _randomness.Dispose();
}
