using System;
using System.Collections.Generic;
using Janus.Core;
using Janus.Identity.Identifiers;
using Xunit;

namespace Janus.Identity.Tests.Identifiers;

/// <summary>
/// The rules that hold across an account's identifiers: one primary per kind, how many
/// of a kind an account may hold, and who a security notice reaches
/// (REG-IDENT-001, REG-IDENT-002, REG-IDENT-005).
/// </summary>
[Trait("kind", "unit")]
public sealed class IdentifierSetTests
{
    private static readonly SubjectId Ahmed = new(Guid.Parse("11111111-1111-4111-8111-111111111111"));
    private static readonly SubjectId Mona = new(Guid.Parse("22222222-2222-4222-8222-222222222222"));
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// REG-IDENT-002 AC1: a kind with no verified identifier has no primary, and the
    /// first of the kind to verify takes the role.
    /// </summary>
    [Fact]
    public void REG_IDENT_002_AC1_TheFirstVerifiedOfAKindBecomesItsPrimary()
    {
        IdentifierSet set = Empty();
        Identifier first = Email(1, "ahmed@example.com");

        set.Add(first, maximum: 10);

        Assert.Null(set.Primary(IdentifierKind.Email));

        set.Verify(first.Id, Noon);

        Assert.Same(first, set.Primary(IdentifierKind.Email));
    }

    /// <summary>
    /// REG-IDENT-002 AC1: exactly one primary exists per kind, so a second verified
    /// identifier does not take the role and promoting it displaces the first.
    /// </summary>
    [Fact]
    public void REG_IDENT_002_AC1_ExactlyOnePrimaryExistsPerKind()
    {
        IdentifierSet set = Empty();
        Identifier first = Email(1, "ahmed@example.com");
        Identifier second = Email(2, "ahmed@example.org");

        set.Add(first, maximum: 10);
        set.Add(second, maximum: 10);
        set.Verify(first.Id, Noon);
        set.Verify(second.Id, Noon);

        Assert.True(first.IsPrimary);
        Assert.False(second.IsPrimary);

        set.MakePrimary(second.Id);

        Assert.False(first.IsPrimary);
        Assert.True(second.IsPrimary);
    }

    /// <summary>
    /// REG-IDENT-002 AC1: each kind has its own primary, so verifying a phone leaves
    /// the primary email where it was.
    /// </summary>
    [Fact]
    public void REG_IDENT_002_AC1_EachKindCarriesItsOwnPrimary()
    {
        IdentifierSet set = Empty();
        Identifier email = Email(1, "ahmed@example.com");
        Identifier phone = Phone(2, "+201001234567");

        set.Add(email, maximum: 10);
        set.Add(phone, maximum: 10);
        set.Verify(email.Id, Noon);
        set.Verify(phone.Id, Noon);

        Assert.Same(email, set.Primary(IdentifierKind.Email));
        Assert.Same(phone, set.Primary(IdentifierKind.Phone));
    }

    /// <summary>
    /// REG-IDENT-005 AC2: an identifier nobody has confirmed is not made primary.
    /// </summary>
    [Fact]
    public void REG_IDENT_005_AC2_AnUnverifiedIdentifierIsNotMadePrimary()
    {
        IdentifierSet set = Empty();
        Identifier email = Email(1, "ahmed@example.com");

        set.Add(email, maximum: 10);

        Assert.Throws<InvalidOperationException>(() => set.MakePrimary(email.Id));
    }

    /// <summary>
    /// REG-IDENT-002: an account holds as many of a kind as its maximum allows and no
    /// more, and never holds one value twice.
    /// </summary>
    [Fact]
    public void REG_IDENT_002_AnAccountHoldsNoMoreOfAKindThanItsMaximum()
    {
        IdentifierSet set = Empty();

        set.Add(Email(1, "ahmed@example.com"), maximum: 2);
        set.Add(Email(2, "ahmed@example.org"), maximum: 2);

        Assert.Throws<InvalidOperationException>(() => set.Add(Email(3, "ahmed@example.net"), maximum: 2));
        Assert.Throws<InvalidOperationException>(() => set.Add(Email(4, "ahmed@example.com"), maximum: 10));
    }

    /// <summary>
    /// REG-IDENT-002: the maximum is per kind, so a full set of emails leaves the
    /// account free to add a phone.
    /// </summary>
    [Fact]
    public void REG_IDENT_002_TheMaximumIsCountedPerKind()
    {
        IdentifierSet set = Empty();

        set.Add(Email(1, "ahmed@example.com"), maximum: 1);
        set.Add(Phone(2, "+201001234567"), maximum: 1);

        Assert.Equal(2, set.All.Count);
    }

    /// <summary>
    /// REG-IDENT-002 AC2: at the default setting a security notice reaches every
    /// verified identifier of the kind, and nothing unverified.
    /// </summary>
    [Fact]
    public void REG_IDENT_002_AC2_TheDefaultSettingReachesEveryVerifiedIdentifier()
    {
        IdentifierSet set = Empty();
        Identifier first = Email(1, "ahmed@example.com");
        Identifier second = Email(2, "ahmed@example.org");
        Identifier unverified = Email(3, "ahmed@example.net");

        set.Add(first, maximum: 10);
        set.Add(second, maximum: 10);
        set.Add(unverified, maximum: 10);
        set.Verify(first.Id, Noon);
        set.Verify(second.Id, Noon);

        Assert.Equal([first, second], set.SecurityNoticeSet(IdentifierKind.Email));
    }

    /// <summary>
    /// REG-IDENT-002 AC2: at the primary-only setting a security notice reaches the
    /// primary and nothing else.
    /// </summary>
    [Fact]
    public void REG_IDENT_002_AC2_ThePrimaryOnlySettingReachesThePrimaryAlone()
    {
        IdentifierSet set = Empty();
        Identifier first = Email(1, "ahmed@example.com");
        Identifier second = Email(2, "ahmed@example.org");

        set.Add(first, maximum: 10);
        set.Add(second, maximum: 10);
        set.Verify(first.Id, Noon);
        set.Verify(second.Id, Noon);
        set.Backup(IdentifierKind.Email).UsePrimaryOnly();

        Assert.Equal([first], set.SecurityNoticeSet(IdentifierKind.Email));
    }

    /// <summary>
    /// REG-IDENT-002 AC2: a named setting reaches the primary and the one it names, and
    /// leaves the rest out.
    /// </summary>
    [Fact]
    public void REG_IDENT_002_AC2_ANamedSettingReachesThePrimaryAndTheOneItNames()
    {
        IdentifierSet set = Empty();
        Identifier first = Email(1, "ahmed@example.com");
        Identifier second = Email(2, "ahmed@example.org");
        Identifier third = Email(3, "ahmed@example.net");

        set.Add(first, maximum: 10);
        set.Add(second, maximum: 10);
        set.Add(third, maximum: 10);
        set.Verify(first.Id, Noon);
        set.Verify(second.Id, Noon);
        set.Verify(third.Id, Noon);
        set.Backup(IdentifierKind.Email).UseNamed(third.Id);

        Assert.Equal([first, third], set.SecurityNoticeSet(IdentifierKind.Email));
    }

    /// <summary>
    /// REG-IDENT-002: the security-notice set is per kind, so the setting for one kind
    /// says nothing about another.
    /// </summary>
    [Fact]
    public void REG_IDENT_002_TheSecurityNoticeSetIsPerKind()
    {
        IdentifierSet set = Empty();
        Identifier email = Email(1, "ahmed@example.com");
        Identifier first = Phone(2, "+201001234567");
        Identifier second = Phone(3, "+201007654321");

        set.Add(email, maximum: 10);
        set.Add(first, maximum: 10);
        set.Add(second, maximum: 10);
        set.Verify(email.Id, Noon);
        set.Verify(first.Id, Noon);
        set.Verify(second.Id, Noon);
        set.Backup(IdentifierKind.Email).UsePrimaryOnly();

        Assert.Equal([email], set.SecurityNoticeSet(IdentifierKind.Email));
        Assert.Equal([first, second], set.SecurityNoticeSet(IdentifierKind.Phone));
    }

    /// <summary>
    /// REG-MAIL-001 AC5: the personal email a membership keeps is reached by every
    /// security notice whatever the backup setting, and is neither made primary,
    /// removed nor replaced while it is kept.
    /// </summary>
    [Fact]
    public void REG_MAIL_001_AC5_ThePersonalEmailStaysVerifiedNonPrimaryAndNotified()
    {
        IdentifierSet set = Empty();
        Identifier personal = Email(1, "ahmed@example.com");
        Identifier corporate = Email(2, "ahmed@staff.example");

        set.Add(personal, maximum: 10);
        set.Add(corporate, maximum: 10);
        set.Verify(personal.Id, Noon);
        set.Verify(corporate.Id, Noon);
        set.MakePrimary(corporate.Id);
        set.KeepPersonal(personal.Id);
        set.Backup(IdentifierKind.Email).UsePrimaryOnly();

        Assert.Equal([personal, corporate], set.SecurityNoticeSet(IdentifierKind.Email));
        Assert.Equal([personal, corporate], set.SecurityNoticeSet());
        Assert.Throws<InvalidOperationException>(() => set.MakePrimary(personal.Id));
        Assert.Throws<InvalidOperationException>(() => set.Remove(personal.Id));
        Assert.Throws<InvalidOperationException>(
            () => personal.Replace("ahmed@example.org", "ahmed@example.org", Noon));
        Assert.True(corporate.IsPrimary);
        Assert.Equal([personal, corporate], set.All);
    }

    /// <summary>
    /// REG-MAIL-003 AC2 and AC3: when the membership ends the personal email becomes the
    /// primary, kept no longer, and the corporate address leaves the account, so the
    /// account never holds zero verified emails; an account whose corporate address is
    /// already gone still continues on its personal email.
    /// </summary>
    [Fact]
    public void REG_MAIL_003_AC2_ThePersonalEmailBecomesPrimaryAndTheCorporateAddressLeaves()
    {
        IdentifierSet set = Empty();
        Identifier personal = Email(1, "ahmed@example.com");
        Identifier corporate = Email(2, "ahmed@staff.example");
        Identifier phone = Phone(3, "+201001234567");

        set.Add(personal, maximum: 10);
        set.Add(corporate, maximum: 10);
        set.Add(phone, maximum: 10);
        set.Verify(personal.Id, Noon);
        set.Verify(corporate.Id, Noon);
        set.Verify(phone.Id, Noon);
        set.MakePrimary(corporate.Id);
        set.KeepPersonal(personal.Id);

        Assert.Equal(personal.Id, set.RetireCorporate(corporate.Canonical));
        Assert.True(personal is { IsPrimary: true, IsPersonal: false, IsVerified: true });
        Assert.Equal([personal, phone], set.All);
        Assert.Equal([corporate.Id], set.Removed);
        Assert.True(phone.IsPrimary);

        IdentifierSet gone = Empty();
        Identifier kept = Email(4, "hana@example.com");
        Identifier other = Email(5, "hana@example.org");

        gone.Add(kept, maximum: 10);
        gone.Add(other, maximum: 10);
        gone.Verify(kept.Id, Noon);
        gone.Verify(other.Id, Noon);
        gone.MakePrimary(other.Id);
        gone.KeepPersonal(kept.Id);

        Assert.Equal(kept.Id, gone.RetireCorporate("hana@staff.example"));
        Assert.True(kept.IsPrimary);
        Assert.False(other.IsPrimary);
        Assert.Empty(gone.Removed);
        Assert.Throws<InvalidOperationException>(() => Empty().RetireCorporate("hana@staff.example"));
    }

    /// <summary>
    /// REG-MAIL-003 AC3: no path through the set leaves a member with zero verified
    /// emails: the kept personal email can be neither removed nor unverified-replaced
    /// while the membership lasts, retiring the corporate address without it is
    /// refused and changes nothing, and after retirement a verified primary email
    /// remains.
    /// </summary>
    [Fact]
    public void REG_MAIL_003_AC3_MembershipEndNeverLeavesZeroVerifiedEmails()
    {
        IdentifierSet set = Empty();
        Identifier personal = Email(1, "ahmed@example.com");
        Identifier corporate = Email(2, "ahmed@staff.example");

        set.Add(personal, maximum: 10);
        set.Add(corporate, maximum: 10);
        set.Verify(personal.Id, Noon);
        set.Verify(corporate.Id, Noon);
        set.MakePrimary(corporate.Id);

        IdentifierSet unkept = Empty();
        Identifier only = Email(3, "hana@staff.example");

        unkept.Add(only, maximum: 10);
        unkept.Verify(only.Id, Noon);

        Assert.Throws<InvalidOperationException>(() => unkept.RetireCorporate(only.Canonical));
        Assert.Equal([only], unkept.All);
        Assert.True(only is { IsVerified: true, IsPrimary: true });
        Assert.Empty(unkept.Removed);

        set.KeepPersonal(personal.Id);

        Assert.Throws<InvalidOperationException>(() => set.Remove(personal.Id));
        Assert.Throws<InvalidOperationException>(
            () => personal.Replace("ahmed@example.org", "ahmed@example.org", Noon));

        _ = set.RetireCorporate(corporate.Canonical);

        Identifier remaining = Assert.Single(set.All);

        Assert.True(remaining is { IsVerified: true, IsPrimary: true, IsPersonal: false });
        Assert.Same(personal, remaining);
    }

    /// <summary>
    /// REG-MAIL-001: only a verified email other than the primary is kept as the
    /// personal email of a membership.
    /// </summary>
    [Fact]
    public void REG_MAIL_001_OnlyAVerifiedEmailOtherThanThePrimaryIsKept()
    {
        IdentifierSet set = Empty();
        Identifier primary = Email(1, "ahmed@example.com");
        Identifier unverified = Email(2, "ahmed@example.org");
        Identifier phone = Phone(3, "+201001234567");

        set.Add(primary, maximum: 10);
        set.Add(unverified, maximum: 10);
        set.Add(phone, maximum: 10);
        set.Verify(primary.Id, Noon);
        set.Verify(phone.Id, Noon);

        Assert.Throws<InvalidOperationException>(() => set.KeepPersonal(primary.Id));
        Assert.Throws<InvalidOperationException>(() => set.KeepPersonal(unverified.Id));
        Assert.Throws<InvalidOperationException>(() => set.KeepPersonal(phone.Id));
        Assert.DoesNotContain(set.All, identifier => identifier.IsPersonal);
    }

    /// <summary>
    /// An identifier of another account never joins this one's set, whether it is read
    /// with the set or added to it.
    /// </summary>
    [Fact]
    public void Of_IdentifierOfAnotherAccount_Throws()
    {
        var stranger = Identifier.Email(
            Id(9),
            Mona,
            Address("mona@example.com"),
            "mona@example.com",
            Noon);

        Assert.Throws<ArgumentException>(() => IdentifierSet.Of(Ahmed, [stranger], []));
        Assert.Throws<ArgumentException>(() => Empty().Add(stranger, maximum: 10));
    }

    /// <summary>
    /// IDN-LIFE-012a: the address a provider stopped forwarding to drops to unverified,
    /// and where it was the primary the earliest verified email takes the role; an
    /// address already unverified, and the personal email a membership keeps, are
    /// refused and left as they were (REG-MAIL-001 AC5).
    /// </summary>
    [Fact]
    public void IDN_LIFE_012a_AnUnvouchedAddressDropsToUnverifiedAndHandsThePrimaryOn()
    {
        IdentifierSet set = Empty();
        Identifier relayed = Email(1, "x7@relay.example");
        Identifier later = Email(2, "ahmed@example.org");
        Identifier earlier = Email(3, "ahmed@example.com");
        Identifier kept = Email(4, "ahmed@example.net");

        set.Add(relayed, maximum: 10);
        set.Add(later, maximum: 10);
        set.Add(earlier, maximum: 10);
        set.Add(kept, maximum: 10);
        set.Verify(kept.Id, Noon.AddMinutes(-3));
        set.Verify(earlier.Id, Noon.AddMinutes(-2));
        set.Verify(later.Id, Noon.AddMinutes(-1));
        set.Verify(relayed.Id, Noon);
        set.MakePrimary(relayed.Id);
        set.KeepPersonal(kept.Id);

        set.Unverify(relayed.Id);

        Assert.True(relayed is { IsVerified: false, IsPrimary: false });
        Assert.True(earlier.IsPrimary);
        Assert.False(later.IsPrimary);
        Assert.Throws<InvalidOperationException>(() => set.Unverify(relayed.Id));
        Assert.Throws<InvalidOperationException>(() => set.Unverify(kept.Id));
        Assert.True(kept is { IsVerified: true, IsPersonal: true });
    }

    private static IdentifierSet Empty() =>
        IdentifierSet.Of(Ahmed, [], new List<BackupSetting>());

    private static Identifier Email(int number, string address) =>
        Identifier.Email(Id(number), Ahmed, Address(address), address, Noon);

    private static Identifier Phone(int number, string value)
    {
        Assert.True(PhoneNumber.TryParse(value, out PhoneNumber parsed));

        return Identifier.Phone(Id(number), Ahmed, parsed, value, Noon);
    }

    private static EmailAddress Address(string value)
    {
        Assert.True(EmailAddress.TryParse(value, out EmailAddress parsed));

        return parsed;
    }

    private static IdentifierId Id(int number) =>
        new(new Guid(number, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]));
}
