using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Npgsql;

namespace Janus.Storage.Tests;

/// <summary>
/// The search of the library's schema for a key in any form a writer could have put it
/// in, which the fingerprint key's absence is proved by (INF-HOST-003).
/// </summary>
public static class FingerprintKeyScan
{
    /// <summary>
    /// The forms a key could take in a column: its bytes, which a bytea column reads back
    /// as hexadecimal, and the encodings a writer could have put in a text or JSON column,
    /// unpadded so a padded copy is found as well.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The forms.</returns>
    public static IEnumerable<string> WrittenForms(byte[] key)
    {
        string base64 = Convert.ToBase64String(key).TrimEnd('=');

        return
        [
            Convert.ToHexStringLower(key),
            Convert.ToHexString(key),
            base64,
            base64.Replace("+", "\\u002B", StringComparison.Ordinal),
            Base64Url.EncodeToString(key),
        ];
    }

    /// <summary>
    /// Every column of every base table in the library's schema whose value, read back
    /// as text, contains any of the forms, by its table and name.
    /// </summary>
    /// <param name="connection">An open connection to the database.</param>
    /// <param name="forms">The forms looked for.</param>
    /// <returns>The columns holding one.</returns>
    public static async Task<IReadOnlyList<string>> HoldingAsync(NpgsqlConnection connection, string[] forms)
    {
        IReadOnlyList<(string Table, string Column)> columns = [.. await connection.QueryAsync<(string, string)>(
            """
            SELECT quote_ident(c.table_name), quote_ident(c.column_name)
            FROM information_schema.columns c
            JOIN information_schema.tables t
              ON t.table_schema = c.table_schema AND t.table_name = c.table_name
            WHERE c.table_schema = 'identity' AND t.table_type = 'BASE TABLE'
            ORDER BY c.table_name, c.column_name
            """)];

        var holding = new List<string>();

        foreach ((string table, string column) in columns)
        {
            bool held = await connection.ExecuteScalarAsync<bool>(
                $"""
                SELECT EXISTS (
                    SELECT 1 FROM identity.{table} AS stored, unnest(@forms) AS form
                    WHERE strpos(stored.{column}::text, form) > 0)
                """,
                new { forms });

            if (held)
            {
                holding.Add(table + "." + column);
            }
        }

        return holding;
    }
}
