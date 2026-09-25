using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Privacy;

/// <summary>
/// How long a category of data the host declared is kept: the period the deployment
/// stated for it, or the floor the host declared where it stated none.
/// </summary>
/// <param name="declaration">What the host declared about its own domain.</param>
/// <param name="configuration">Where the stated period is read.</param>
/// <remarks>
/// Implements PRIV-RET-001 and chapter 10 section 4. A stated period below the floor
/// is refused rather than raised to it, so the deployment that stated it is told.
/// </remarks>
internal sealed class CategoryRetention(
    AuthorizationDeclaration declaration,
    IConfigurationStore configuration)
{
    /// <summary>
    /// Reads the period a category is kept for.
    /// </summary>
    /// <param name="category">The category, as a purpose names it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The period, or the failure naming the key: declared with no floor, stated in a
    /// form the key does not take, or stated below the floor.
    /// </returns>
    public async ValueTask<Result<TimeSpan>> ReadAsync(string category, CancellationToken cancellationToken)
    {
        ConfigurationKey key = Settings.HostCategoryRetention.For(category);

        if (!declaration.RetentionFloors.TryGetValue(category, out TimeSpan floor))
        {
            return Result.Failure<TimeSpan>(Error.From(
                ErrorCodes.StartupDeclarationMissing,
                "key",
                JsonSerializer.SerializeToElement(key.ToString())));
        }

        Result<TimeSpan> stated = await configuration
            .ReadAsync(Settings.HostCategoryRetention, category, cancellationToken)
            .ConfigureAwait(false);

        return stated.Match(
            period => period < floor
                ? Result.Failure<TimeSpan>(BelowFloor(key, floor))
                : Result.Success(period),
            error => error.Code == ErrorCodes.StartupDeclarationMissing
                ? Result.Success(floor)
                : Result.Failure<TimeSpan>(error));
    }

    // The shape every refused value carries (OPS-CFG-003): the key, and the floor
    // written as the settings table holds it.
    private static Error BelowFloor(ConfigurationKey key, TimeSpan floor) =>
        new(
            ErrorCodes.ConfigurationValueBelowFloor,
            new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
            {
                ["key"] = JsonSerializer.SerializeToElement(key.ToString()),
                ["floor"] = JsonSerializer.SerializeToElement(Settings.HostCategoryRetention.Write(floor)),
            });
}
