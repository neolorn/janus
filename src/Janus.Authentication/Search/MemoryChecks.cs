using System;
using System.Collections.Generic;
using System.IO;

namespace Janus.Authentication.Search;

/// <summary>
/// What keeps the staff search's index off disk, checked before it is built: the
/// process's cgroup allows no swap, its core-dump limit is 0, .NET's diagnostics port is
/// off and its crash dumps are off.
/// </summary>
/// <remarks>
/// Implements IDN-SRCH-003 (D-256). A check whose source cannot be read, a machine with
/// no cgroup version 2 or no <c>/proc</c> among them, counts as failed: a check that
/// cannot be made is never passed (<c>00</c> section 4, fail closed).
/// </remarks>
internal static class MemoryChecks
{
    private const string CgroupOfProcess = "/proc/self/cgroup";

    private const string CgroupRoot = "/sys/fs/cgroup";

    private const string SwapLimit = "memory.swap.max";

    private const string Limits = "/proc/self/limits";

    private const string CoreLimit = "Max core file size";

    private const string Diagnostics = "DOTNET_EnableDiagnostics";

    private const string MiniDump = "DOTNET_DbgEnableMiniDump";

    /// <summary>
    /// The checks this process fails.
    /// </summary>
    /// <returns>Each failed check, in the order the checks are made.</returns>
    public static IReadOnlyList<MemoryCheck> FailedHere() =>
        Failed(
            SwapLimitHere(),
            Read(Limits),
            Environment.GetEnvironmentVariable(Diagnostics),
            Environment.GetEnvironmentVariable(MiniDump));

    /// <summary>
    /// The checks failed by what the process's sources read.
    /// </summary>
    /// <param name="swapLimit">The cgroup's <c>memory.swap.max</c>, or nothing where it cannot be read.</param>
    /// <param name="limits">The process's <c>/proc/self/limits</c>, or nothing where it cannot be read.</param>
    /// <param name="diagnostics">The value of <c>DOTNET_EnableDiagnostics</c>, or nothing where it is unset.</param>
    /// <param name="miniDump">The value of <c>DOTNET_DbgEnableMiniDump</c>, or nothing where it is unset.</param>
    /// <returns>Each failed check, in the order the checks are made.</returns>
    public static IReadOnlyList<MemoryCheck> Failed(string? swapLimit, string? limits, string? diagnostics, string? miniDump)
    {
        var failed = new List<MemoryCheck>(4);

        if (swapLimit?.Trim() is not "0")
        {
            failed.Add(MemoryCheck.Swap);
        }

        if (SoftCoreLimit(limits) is not "0")
        {
            failed.Add(MemoryCheck.CoreDump);
        }

        if (diagnostics?.Trim() is not "0")
        {
            failed.Add(MemoryCheck.Diagnostics);
        }

        if (miniDump?.Trim() is "1")
        {
            failed.Add(MemoryCheck.MiniDump);
        }

        return failed;
    }

    /// <summary>
    /// The name a check is given in <c>details.check</c> and in the log.
    /// </summary>
    /// <param name="check">The check.</param>
    /// <returns>Its name.</returns>
    public static string Name(MemoryCheck check) =>
        check switch
        {
            MemoryCheck.Swap => "swap",
            MemoryCheck.CoreDump => "coredump",
            MemoryCheck.Diagnostics => "diagnostics",
            _ => "minidump",
        };

    // Under cgroup version 2 the process's group is the one line of its cgroup file,
    // `0::<path>`, and the group's swap limit is a file of that path.
    private static string? SwapLimitHere()
    {
        if (Read(CgroupOfProcess) is not string groups)
        {
            return null;
        }

        foreach (string line in groups.Split('\n'))
        {
            if (line.StartsWith("0::", StringComparison.Ordinal))
            {
                return Read(CgroupRoot + line[3..].TrimEnd('/') + "/" + SwapLimit);
            }
        }

        return null;
    }

    // The soft limit of the core-dump size: the first value after the row's name.
    private static string? SoftCoreLimit(string? limits)
    {
        if (limits is null)
        {
            return null;
        }

        foreach (string line in limits.Split('\n'))
        {
            if (line.StartsWith(CoreLimit, StringComparison.Ordinal))
            {
                string[] values = line[CoreLimit.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);

                return values.Length > 0 ? values[0] : null;
            }
        }

        return null;
    }

    private static string? Read(string path) => File.Exists(path) ? File.ReadAllText(path) : null;
}
