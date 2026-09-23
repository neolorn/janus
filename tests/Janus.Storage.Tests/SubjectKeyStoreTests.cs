using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Privacy.SubjectKeys;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// The wrapped data key as the <c>subject_keys</c> row carries it, and what becomes of
/// the fields under it when the row is re-wrapped or erased (PRIV-RIGHT-005a).
/// </summary>
/// <remarks>
/// The port implementation is tested against the aggregate it translates, with the real
/// database (D-156). The ciphertext each test holds in a local stands for a value in a
/// table the library never touches: nothing writes it again, so what the key does to it
/// is what erasure and rotation do to every such value.
/// </remarks>
[Trait("kind", "integration")]
public sealed class SubjectKeyStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private static readonly byte[] Secret =
        Encoding.UTF8.GetBytes("a value held under one subject key");

    private static readonly RandomNumberGenerator Randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// A key written through the store reads back as the key that was written, and the
    /// data key it wrapped still decrypts what was encrypted under it.
    /// </summary>
    [Fact]
    public async Task FindBySubjectAsync_AKeyThatWasWritten_ReadsBackAndStillDecryptsAsync()
    {
        SubjectId subject = Subjects.New();
        KeyEncryptionKeys keys = OneVersion(1);
        PersonalFieldLocation location = Somewhere(subject);

        using var randomness = RandomNumberGenerator.Create();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);
        byte[] stored = PersonalFieldCipher.Encrypt(dataKey, location, Secret, randomness);

        await WriteAsync(subject, dataKey, keys);
        CryptographicOperations.ZeroMemory(dataKey);

        await using StoreContext reading = database.Context();
        SubjectKey read = await ReadAsync(reading, subject);

        Assert.Equal(subject, read.Subject);
        Assert.Equal(keys.CurrentVersion, read.KeyVersion);
        Assert.False(read.IsErased);
        Assert.Equal(Secret, Decrypted(read, keys, location, stored));
    }

    /// <summary>
    /// A subject with no row is nothing to read, not a key of some default shape.
    /// </summary>
    [Fact]
    public async Task FindBySubjectAsync_ASubjectWithNoRow_ReadsNothingAsync()
    {
        await using StoreContext context = database.Context();

        Assert.Null(await Store(context)
            .FindBySubjectAsync(Subjects.New(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC9: overwriting the wrapped key leaves every field encrypted
    /// under it unrecoverable, wherever the field is held and without the application
    /// that holds it doing anything.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC9_AnErasedKeyLeavesItsFieldsUnrecoverableAsync()
    {
        SubjectId subject = Subjects.New();
        KeyEncryptionKeys keys = OneVersion(1);
        PersonalFieldLocation location = Somewhere(subject);

        using var randomness = RandomNumberGenerator.Create();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);
        byte[] stored = PersonalFieldCipher.Encrypt(dataKey, location, Secret, randomness);
        byte[] untouched = (byte[])stored.Clone();

        await WriteAsync(subject, dataKey, keys);
        CryptographicOperations.ZeroMemory(dataKey);

        await using (StoreContext erasing = database.Context())
        {
            SubjectKey key = await ReadAsync(erasing, subject);
            key.Erase();

            await Store(erasing)
                .RecordWrappingAsync(key, TestContext.Current.CancellationToken);
            await erasing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        SubjectKey erased = await ReadAsync(reading, subject);

        Assert.True(erased.IsErased);
        Assert.Equal(untouched, stored);
        Assert.Throws<CryptographicException>(() => Decrypted(erased, keys, location, stored));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC13: a rotation re-wraps the key under the new version and
    /// re-encrypts nothing, so the value stored before it decrypts after it, byte for
    /// byte the value that was written.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC13_AReWrappedKeyReadsTheValuesWrittenBeforeItAsync()
    {
        SubjectId subject = Subjects.New();
        KeyEncryptionKeys first = OneVersion(1);
        KeyEncryptionKeys second = OneVersion(2);
        PersonalFieldLocation location = Somewhere(subject);

        using var randomness = RandomNumberGenerator.Create();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);
        byte[] stored = PersonalFieldCipher.Encrypt(dataKey, location, Secret, randomness);
        byte[] untouched = (byte[])stored.Clone();

        await WriteAsync(subject, dataKey, first);
        CryptographicOperations.ZeroMemory(dataKey);

        await using (StoreContext rotating = database.Context())
        {
            SubjectKey key = await ReadAsync(rotating, subject);
            byte[] unwrapped = Unwrapped(key, first);

            try
            {
                key.ReWrap(second.CurrentVersion, PersonalFieldCipher.Wrap(unwrapped, second.Current.Span));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(unwrapped);
            }

            await Store(rotating)
                .RecordWrappingAsync(key, TestContext.Current.CancellationToken);
            await rotating.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        SubjectKey rotated = await ReadAsync(reading, subject);

        Assert.Equal(second.CurrentVersion, rotated.KeyVersion);
        Assert.Equal(untouched, stored);
        Assert.Equal(Secret, Decrypted(rotated, second, location, stored));
    }

    /// <summary>
    /// A wrapping recorded for a subject that has no row is a fault in the caller, not a
    /// write that quietly creates one.
    /// </summary>
    [Fact]
    public async Task RecordWrappingAsync_ASubjectWithNoRow_ThrowsAsync()
    {
        await using StoreContext context = database.Context();

        var key = SubjectKey.Wrapped(
            Subjects.New(),
            1,
            new byte[PersonalDataFormat.WrappedKeyLength]);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store(context)
                .RecordWrappingAsync(key, TestContext.Current.CancellationToken));
    }

    private static PersonalFieldLocation Somewhere(SubjectId subject) =>
        new(subject, "orders", "enc_recipient_name");

    private static KeyEncryptionKeys OneVersion(int version)
    {
        using var randomness = RandomNumberGenerator.Create();
        byte[] material = new byte[PersonalDataFormat.DataKeyLength];
        randomness.GetBytes(material);

        return new KeyEncryptionKeys(
            version,
            new Dictionary<int, ReadOnlyMemory<byte>> { [version] = material });
    }

    private static byte[] Unwrapped(SubjectKey key, KeyEncryptionKeys keys) =>
        PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey.Span, keys);

    private static byte[] Decrypted(
        SubjectKey key,
        KeyEncryptionKeys keys,
        in PersonalFieldLocation location,
        ReadOnlySpan<byte> stored)
    {
        byte[] dataKey = Unwrapped(key, keys);

        try
        {
            return PersonalFieldCipher.Decrypt(dataKey, location, stored);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private static async Task<SubjectKey> ReadAsync(StoreContext context, SubjectId subject) =>
        Assert.IsType<SubjectKey>(await Store(context)
            .FindBySubjectAsync(subject, TestContext.Current.CancellationToken));

    private async Task WriteAsync(SubjectId subject, byte[] dataKey, KeyEncryptionKeys keys)
    {
        await using StoreContext context = database.Context();

        await Store(context).AddAsync(
            SubjectKey.Wrapped(
                subject,
                keys.CurrentVersion,
                PersonalFieldCipher.Wrap(dataKey, keys.Current.Span)),
            TestContext.Current.CancellationToken);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // The store draws and wraps a key of its own; the tests here write the wrapping
    // they mean to test, so the versions and the randomness it would draw with are
    // whatever a store needs to be constructed.
    private static SubjectKeyStore Store(StoreContext context) =>
        new(context, OneVersion(1), Randomness);
}
