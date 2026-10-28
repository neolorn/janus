using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Core;
using Janus.Storage.Authentication.Records;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Identity.Records;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Privacy;

/// <summary>
/// The cipher of a host's own declared fields over the subject keys the deployment holds
/// (PRIV-RIGHT-005a criteria 20 and 21, D-203).
/// </summary>
/// <remarks>
/// The host here is a courier whose parcels name their recipient, an invented domain.
/// Each test draws its own subjects, so what one writes is unreadable to another.
/// </remarks>
[Trait("kind", "integration")]
public sealed class PersonalFieldsTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly ResourceType ParcelType = ResourceType.Parse("parcel");

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// PRIV-RIGHT-005a AC20: a text field and a binary field written through the cipher
    /// read back under their subject, type and field, and neither is held in plain.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC20_AHostFieldWrittenThroughTheCipherReadsBackUnderItsSubjectAsync()
    {
        SubjectId recipient = await _deployment.AccountAsync(Noon);
        byte[] signature = RandomNumberGenerator.GetBytes(64);

        (ReadOnlyMemory<byte> name, ReadOnlyMemory<byte> signed) = await WrittenAsync(
            recipient,
            fields => (Value(fields.Encrypt(ParcelType, "RecipientName", "Named Recipient")),
                Value(fields.Encrypt(ParcelType, "Signature", signature))));

        await using StoreContext reading = database.Context();
        (string readName, ReadOnlyMemory<byte> readSignature) = Value(await Fields(reading).WithSubjectAsync(
            recipient,
            fields => Result.Success((
                Value(fields.Decrypt(ParcelType, "RecipientName", name)),
                Value(fields.DecryptBytes(ParcelType, "Signature", signed)))),
            TestContext.Current.CancellationToken));

        Assert.Equal("Named Recipient", readName);
        Assert.Equal(signature, readSignature.ToArray());
        Assert.Equal(-1, name.Span.IndexOf("Named Recipient"u8));
        Assert.Equal(0x01, name.Span[0]);
    }

    /// <summary>
    /// REC-ATT-001 AC4: a field written under a person record's subject still decrypts
    /// under that subject once the record is attached to an account, since attaching
    /// moves and re-encrypts nothing.
    /// </summary>
    [Fact]
    public async Task REC_ATT_001_AC4_AFieldOfARecordStillDecryptsOnceItIsAttachedAsync()
    {
        SubjectId record = await RecordAsync();
        SubjectId account = await _deployment.AccountAsync(Noon);

        (ReadOnlyMemory<byte> name, _) = await WrittenAsync(
            record,
            fields => (Value(fields.Encrypt(ParcelType, "RecipientName", "Named Recipient")), default));

        await using (StoreContext attaching = database.Context())
        {
            await using var work = new UnitOfWork(attaching);

            await work.BeginAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(await Directory(attaching).AttachAsync(
                record,
                account,
                Noon.AddDays(1),
                TestContext.Current.CancellationToken));
            await work.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Equal(
            [record],
            (await Directory(reading).AttachedToAsync(account, TestContext.Current.CancellationToken))
                .Select(attached => attached.Record));
        Assert.Equal("Named Recipient", Value(await Fields(reading).WithSubjectAsync(
            record,
            fields => fields.Decrypt(ParcelType, "RecipientName", name),
            TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC20: a field's declared name is its member's, or the one the
    /// declaration gives it, so a member the host renamed and gave its old name reads
    /// what was written under that name.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC20_AMemberGivenItsOldNameReadsWhatWasWrittenUnderItAsync()
    {
        SubjectId recipient = await _deployment.AccountAsync(Noon);

        (ReadOnlyMemory<byte> stored, _) = await WrittenAsync(
            recipient,
            fields => (Value(fields.Encrypt(ParcelType, "RecipientName", "Named Recipient")), default));

        AuthorizationDeclaration renamed = new AuthorizationDeclarationBuilder()
            .Resource<Parcel>("parcel", parcel => parcel
                .Purpose("delivery", "contract", data: ["contact"])
                .Encrypted(row => row.Addressee, row => row.Recipient, "contact", name: "RecipientName"))
            .Build();

        await using StoreContext reading = database.Context();
        string read = Value(await Fields(reading, renamed).WithSubjectAsync(
            recipient,
            fields => fields.Decrypt(ParcelType, "RecipientName", stored),
            TestContext.Current.CancellationToken));

        Assert.Equal("Named Recipient", read);
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC20: a value moved onto another subject's record does not
    /// authenticate under that subject, and that is a broken invariant, never a refusal.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC20_AValueMovedToAnotherSubjectDoesNotDecryptAsync()
    {
        SubjectId recipient = await _deployment.AccountAsync(Noon);
        SubjectId other = await _deployment.AccountAsync(Noon);

        (ReadOnlyMemory<byte> stored, _) = await WrittenAsync(
            recipient,
            fields => (Value(fields.Encrypt(ParcelType, "RecipientName", "Named Recipient")), default));

        await using StoreContext reading = database.Context();

        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await Fields(reading).WithSubjectAsync(
                other,
                fields => fields.Decrypt(ParcelType, "RecipientName", stored),
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC20: an altered value is a broken invariant, never a refusal.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC20_AnAlteredValueIsAFaultAsync()
    {
        SubjectId recipient = await _deployment.AccountAsync(Noon);

        (ReadOnlyMemory<byte> stored, _) = await WrittenAsync(
            recipient,
            fields => (Value(fields.Encrypt(ParcelType, "RecipientName", "Named Recipient")), default));
        byte[] altered = stored.ToArray();
        altered[^1] ^= 0x01;

        await using StoreContext reading = database.Context();

        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await Fields(reading).WithSubjectAsync(
                recipient,
                fields => fields.Decrypt(ParcelType, "RecipientName", altered),
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC20: once the subject is erased the field answers
    /// <c>privacy.subject.erased</c>, and the view says the key is erased.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC20_AfterErasureAFieldAnswersSubjectErasedAsync()
    {
        SubjectId recipient = await _deployment.AccountAsync(Noon);

        (ReadOnlyMemory<byte> stored, _) = await WrittenAsync(
            recipient,
            fields => (Value(fields.Encrypt(ParcelType, "RecipientName", "Named Recipient")), default));
        await _deployment.EraseAsync(recipient);

        await using StoreContext reading = database.Context();
        (bool erased, ErrorCode? text, ErrorCode? bytes) = Value(await Fields(reading).WithSubjectAsync(
            recipient,
            fields => Result.Success((
                fields.Erased,
                Code(fields.Decrypt(ParcelType, "RecipientName", stored)),
                Code(fields.DecryptBytes(ParcelType, "Signature", stored)))),
            TestContext.Current.CancellationToken));

        Assert.True(erased);
        Assert.Equal(ErrorCodes.SubjectErased, text);
        Assert.Equal(ErrorCodes.SubjectErased, bytes);
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC20: several fields of a page of subjects are read with one
    /// statement for the keys and one unwrap per subject.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC20_APageIsReadWithOneStatementAndOneUnwrapPerSubjectAsync()
    {
        SubjectId[] recipients =
        [
            await _deployment.AccountAsync(Noon),
            await _deployment.AccountAsync(Noon),
            await _deployment.AccountAsync(Noon),
        ];
        var written = new Dictionary<SubjectId, (ReadOnlyMemory<byte> Name, ReadOnlyMemory<byte> Phone)>();

        foreach (SubjectId recipient in recipients)
        {
            written[recipient] = await WrittenAsync(
                recipient,
                fields => (Value(fields.Encrypt(ParcelType, "RecipientName", "Recipient " + recipient)),
                    Value(fields.Encrypt(ParcelType, "RecipientPhone", "000 0000"))));
        }

        var statements = new CountedCommands();
        var unwraps = new CountingVersions(_deployment.Versions);

        await using StoreContext reading = new(new DbContextOptionsBuilder<StoreContext>()
            .UseNpgsql(database.ConnectionString)
            .AddInterceptors(statements)
            .Options);
        var fields = new PersonalFields(
            reading,
            new KeyRingInMemory(new KeyEncryptionKeys(1, unwraps), Deployment.FingerprintKeys),
            _deployment.Randomness,
            Declared());

        IReadOnlyList<string> names = Value(await fields.WithSubjectsAsync(
            recipients,
            page => Result.Success<IReadOnlyList<string>>(
            [
                .. recipients.SelectMany(recipient => new[]
                {
                    Value(page[recipient].Decrypt(ParcelType, "RecipientName", written[recipient].Name)),
                    Value(page[recipient].Decrypt(ParcelType, "RecipientPhone", written[recipient].Phone)),
                }),
            ]),
            TestContext.Current.CancellationToken));

        Assert.Equal(6, names.Count);
        Assert.Equal("Recipient " + recipients[2], names[4]);
        Assert.Equal(1, statements.Statements);
        Assert.Equal(recipients.Length, unwraps.Reads);
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC21: a host's field is not encrypted for an erased subject.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC21_EncryptingForAnErasedSubjectIsRefusedAsync()
    {
        SubjectId recipient = await _deployment.AccountAsync(Noon);
        await _deployment.EraseAsync(recipient);

        await using StoreContext writing = database.Context();
        (ErrorCode? text, ErrorCode? bytes) = Value(await Fields(writing).WithSubjectAsync(
            recipient,
            fields => Result.Success((
                Code(fields.Encrypt(ParcelType, "RecipientName", "Named Recipient")),
                Code(fields.Encrypt(ParcelType, "Signature", new byte[] { 1, 2, 3 })))),
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.SubjectErased, text);
        Assert.Equal(ErrorCodes.SubjectErased, bytes);
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC21: opening the fields of a subject the deployment holds no key
    /// for is refused with <c>api.request.invalid</c> naming <c>subject</c>; a page is
    /// judged whole, so one such subject among others refuses the page and the use is
    /// never called.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC21_ASubjectWithNoKeyIsRefusedNamingSubjectAsync()
    {
        SubjectId held = await _deployment.AccountAsync(Noon);
        SubjectId unknown = Subjects.New();
        bool used = false;

        await using StoreContext reading = database.Context();
        Result<bool> alone = await Fields(reading).WithSubjectAsync(
            unknown,
            _ => Result.Success(used = true),
            TestContext.Current.CancellationToken);
        Result<bool> page = await Fields(reading).WithSubjectsAsync(
            [held, unknown],
            _ => Result.Success(used = true),
            TestContext.Current.CancellationToken);

        foreach (Result<bool> refused in new[] { alone, page })
        {
            Error error = refused.Match(_ => throw new InvalidOperationException("The fields were lent."), failure => failure);

            Assert.Equal(ErrorCodes.RequestInvalid, error.Code);
            Assert.Equal("subject", error.Details["member"].GetString());
        }

        Assert.False(used);
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC20: a field its type does not declare encrypted, or a type the
    /// host does not declare, is a fault of the calling code, never a refusal.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC20_AFieldItsTypeDoesNotDeclareEncryptedIsAFaultAsync()
    {
        SubjectId recipient = await _deployment.AccountAsync(Noon);

        await using StoreContext writing = database.Context();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Fields(writing).WithSubjectAsync(
                recipient,
                fields => fields.Encrypt(ParcelType, "Weight", "2 kg"),
                TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Fields(writing).WithSubjectAsync(
                recipient,
                fields => fields.Encrypt(ResourceType.Parse("depot"), "RecipientName", "Named Recipient"),
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC20: the key is lent for the length of the use, so a view kept
    /// past it is a fault whatever is asked of it.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC20_AViewUsedAfterTheUseReturnedIsAFaultAsync()
    {
        SubjectId recipient = await _deployment.AccountAsync(Noon);

        await using StoreContext reading = database.Context();
        SubjectFields kept = Value(await Fields(reading).WithSubjectAsync(
            recipient,
            Result.Success,
            TestContext.Current.CancellationToken));

        Assert.Throws<ObjectDisposedException>(() => kept.Erased);
        Assert.Throws<ObjectDisposedException>(() => kept.Encrypt(ParcelType, "RecipientName", "Named Recipient"));
        Assert.Throws<ObjectDisposedException>(() => kept.Decrypt(ParcelType, "RecipientName", new byte[29]));
        Assert.Throws<ObjectDisposedException>(() => kept.DecryptBytes(ParcelType, "Signature", new byte[29]));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC20: a use that throws still returns the key it was lent.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC20_AUseThatThrowsStillReturnsTheKeyAsync()
    {
        SubjectId recipient = await _deployment.AccountAsync(Noon);
        SubjectFields? kept = null;

        await using StoreContext reading = database.Context();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Fields(reading).WithSubjectAsync<bool>(
                recipient,
                fields =>
                {
                    kept = fields;

                    throw new InvalidOperationException("The host's use failed.");
                },
                TestContext.Current.CancellationToken));

        Assert.NotNull(kept);
        Assert.Throws<ObjectDisposedException>(() => kept.Erased);
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static TValue Value<TValue>(Result<TValue> result) =>
        result.Match(value => value, failure => throw new InvalidOperationException(failure.Code.ToString()));

    private static ErrorCode? Code<TValue>(Result<TValue> result) =>
        result.Match<ErrorCode?>(_ => null, failure => failure.Code);

    private static AuthorizationDeclaration Declared() =>
        new AuthorizationDeclarationBuilder()
            .Resource<Parcel>("parcel", parcel => parcel
                .Purpose("delivery", "contract", data: ["contact"])
                .Encrypted(row => row.RecipientName, row => row.Recipient, "contact")
                .Encrypted(row => row.RecipientPhone, row => row.Recipient, "contact")
                .Encrypted(row => row.Signature, row => row.Recipient, "contact"))
            .Build();

    private RecordDirectory Directory(StoreContext context) =>
        new(
            context,
            new PersonRecordStore(context, _deployment.Ring, _deployment.Randomness),
            new IdentifierStore(context, _deployment.Ring, _deployment.Randomness),
            new SubjectKeyStore(context, _deployment.Ring, _deployment.Randomness));

    // A person record a collection wrote, of a contact verified then, under a key of its
    // own subject.
    private async Task<SubjectId> RecordAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId subject = Subjects.New();

        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);

        await work.BeginAsync(TestContext.Current.CancellationToken);
        await Directory(writing).WriteAsync(
            new CollectedPerson(
                subject,
                organization,
                Created: true,
                "en",
                Name: null,
                Age: null,
                [new CollectedContact(IdentifierKind.Phone, "+441632960047", Noon)],
                Reference: null,
                "1",
                TermsVersion: null,
                Noon),
            TestContext.Current.CancellationToken);
        await work.CommitAsync(TestContext.Current.CancellationToken);

        return subject;
    }

    private PersonalFields Fields(StoreContext context) => Fields(context, Declared());

    private PersonalFields Fields(StoreContext context, AuthorizationDeclaration declaration) =>
        new(context, _deployment.Ring, _deployment.Randomness, declaration);

    private async Task<(ReadOnlyMemory<byte> First, ReadOnlyMemory<byte> Second)> WrittenAsync(
        SubjectId recipient,
        Func<SubjectFields, (ReadOnlyMemory<byte>, ReadOnlyMemory<byte>)> write)
    {
        await using StoreContext writing = database.Context();

        return Value(await Fields(writing).WithSubjectAsync(
            recipient,
            fields => Result.Success(write(fields)),
            TestContext.Current.CancellationToken));
    }
}
