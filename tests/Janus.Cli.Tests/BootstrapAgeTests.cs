using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Storage.Tests;
using Npgsql;
using Xunit;

namespace Janus.Cli.Tests;

/// <summary>
/// What the <c>bootstrap</c> command derives from the first administrator's date of
/// birth, each case on a fresh database of its own, since a deployment is stood up once
/// (PRIV-MINOR-001, REG-PROF-002).
/// </summary>
[Trait("kind", "integration")]
public sealed class BootstrapAgeTests
{
    /// <summary>
    /// PRIV-MINOR-001 AC3: under the default <c>registration.adultaffirmation</c> of
    /// <c>required</c>, an administrator whose date makes them under eighteen is refused
    /// by the code the age screen refuses with, and nothing is written, so no account
    /// exists whose subject has not affirmed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_MINOR_001_AC3_BootstrapRefusesAnAdministratorUnderEighteenAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using var database = new DatabaseFixture();
        await database.InitializeAsync();

        Invocation refused = await Invocation.PipedAsync(
            Dated(Today().AddYears(-18).AddDays(1)),
            Invocation.Keys(database.ConnectionString));

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(1, refused.ExitCode);
        Assert.Empty(refused.Output);
        Assert.Equal(ErrorCodes.ProfileUnderage.ToString(), Code(refused));

        foreach (string table in (string[])["accounts", "organizations", "settings"])
        {
            Assert.Equal(0, await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                $"SELECT count(*) FROM identity.{table}",
                cancellationToken: cancellationToken)));
        }
    }

    /// <summary>
    /// PRIV-MINOR-001 AC1: the affirmation is the age screen's, so an administrator who
    /// turns eighteen today is an adult and is created having affirmed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_MINOR_001_AC1_AnAdministratorEighteenTodayIsCreatedHavingAffirmedAsync()
    {
        await using var database = new DatabaseFixture();
        await database.InitializeAsync();

        Invocation stood = await Invocation.PipedAsync(
            Dated(Today().AddYears(-18)),
            Invocation.Keys(database.ConnectionString));

        (bool? Affirmed, string? Group) answer = await AdministratorsAnswerAsync(database);

        Assert.Equal(0, stood.ExitCode);
        Assert.Equal((true, null), answer);
    }

    /// <summary>
    /// PRIV-MINOR-001 AC2: where <c>profile.dateofbirth</c> is on, the date the command
    /// was given is kept in the administrator's profile as a personal field under the
    /// subject key, and nowhere else.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_MINOR_001_AC2_WhereTheDateIsKeptBootstrapKeepsItUnderTheSubjectKeyAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using var database = new DatabaseFixture();
        await database.InitializeAsync();
        await SetAsync(database, Settings.ProfileDateOfBirth.Key, Settings.ProfileDateOfBirth.Write(AttributeRequirement.Required));

        Invocation stood = await Invocation.PipedAsync(Invocation.Bootstrap(), Invocation.Keys(database.ConnectionString));

        await using NpgsqlConnection connection = await database.OpenAsync();

        IReadOnlyList<Guid> dated = [.. await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT subject FROM identity.profiles WHERE enc_date_of_birth IS NOT NULL",
            cancellationToken: cancellationToken))];
        Guid administrator = await connection.QuerySingleAsync<Guid>(new CommandDefinition(
            "SELECT holder FROM identity.mailboxes",
            cancellationToken: cancellationToken));
        byte[] sealedDate = await connection.QuerySingleAsync<byte[]>(new CommandDefinition(
            "SELECT enc_date_of_birth FROM identity.profiles WHERE subject = @Subject",
            new { Subject = administrator },
            cancellationToken: cancellationToken));

        Assert.Equal(0, stood.ExitCode);
        Assert.Equal([administrator], dated);
        Assert.DoesNotContain(
            Convert.ToHexString(Encoding.UTF8.GetBytes(Invocation.DateOfBirth)),
            Convert.ToHexString(sealedDate),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// PRIV-MINOR-001 and REG-PROF-002: where <c>registration.adultaffirmation</c> is
    /// off, the date refuses nobody and the band is recorded in place of an affirmation,
    /// as the age screen records it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_MINOR_001_WhereTheAffirmationIsOffTheBandIsRecordedInsteadAsync()
    {
        await using var database = new DatabaseFixture();
        await database.InitializeAsync();
        await SetAsync(
            database,
            Settings.RegistrationAdultAffirmation.Key,
            Settings.RegistrationAdultAffirmation.Write(AttributeRequirement.Off));

        Invocation stood = await Invocation.PipedAsync(
            Dated(Today().AddYears(-16)),
            Invocation.Keys(database.ConnectionString));

        (bool? Affirmed, string? Group) answer = await AdministratorsAnswerAsync(database);

        Assert.Equal(0, stood.ExitCode);
        Assert.Equal((null, "minor"), answer);
    }

    // The day the command runs on, as the clock it reads gives it.
    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    private static List<string> Dated(DateOnly born)
    {
        List<string> arguments = [.. Invocation.Bootstrap()];
        arguments[arguments.IndexOf("--dateofbirth") + 1] = born.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return arguments;
    }

    // A value in place before bootstrap runs, as a deployment's runtime value would be:
    // bootstrap takes only the values that name the deployment.
    private static async Task SetAsync(DatabaseFixture database, ConfigurationKey key, string value)
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO identity.settings (key, value) VALUES (@Key, @Value)",
            new { Key = key.ToString(), Value = value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    private static async Task<(bool? Affirmed, string? Group)> AdministratorsAnswerAsync(DatabaseFixture database)
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        return await connection.QuerySingleAsync<(bool?, string?)>(new CommandDefinition(
            """
            SELECT a.adult_affirmed, a.age_group FROM identity.accounts a
            JOIN identity.mailboxes m ON m.holder = a.subject
            """,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    private static string? Code(Invocation run)
    {
        using var refusal = JsonDocument.Parse(run.Error);

        return refusal.RootElement.GetProperty("code").GetString();
    }
}
