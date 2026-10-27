using System;
using System.Collections.Generic;
using System.Linq;

namespace Janus.Storage;

/// <summary>
/// The statements a block of a migration's script runs, each cut where PostgreSQL ends
/// it: at a semicolon outside a quoted literal, a quoted identifier, a dollar-quoted body
/// and a comment.
/// </summary>
/// <remarks>
/// Implements OPS-MIG-001 and CONV-DESIGN-003, as D-206 settles them: a statement is
/// reported as itself, so a function body or an anonymous block written by hand is one
/// statement, whatever semicolons it holds. The indentation the script gives every line
/// of the block is taken off, and the lines are joined by line feeds, so a statement
/// reads the same whatever line endings the migration was written with.
/// </remarks>
internal static class ScriptStatements
{
    /// <summary>
    /// Cuts a block into its statements.
    /// </summary>
    /// <param name="block">What the block runs.</param>
    /// <returns>Each statement in the order it runs, without its terminating semicolon.</returns>
    /// <exception cref="ArgumentNullException">The block is absent.</exception>
    public static IReadOnlyList<string> Of(string block)
    {
        ArgumentNullException.ThrowIfNull(block);

        string text = Dedented(block.ReplaceLineEndings("\n"));
        var statements = new List<string>();
        int start = 0;
        int at = 0;

        while (at < text.Length)
        {
            char current = text[at];
            char next = at + 1 < text.Length ? text[at + 1] : '\0';

            if (current is '\'' or '"')
            {
                at = Closing(text, at, current);
            }
            else if (current is '-' && next is '-')
            {
                int end = text.IndexOf('\n', at);
                at = end < 0 ? text.Length : end + 1;
            }
            else if (current is '/' && next is '*')
            {
                at = CommentEnd(text, at);
            }
            else if (current is '$' && !(at > 0 && Continues(text[at - 1])) && Tag(text, at) is string tag)
            {
                int end = text.IndexOf(tag, at + tag.Length, StringComparison.Ordinal);
                at = end < 0 ? text.Length : end + tag.Length;
            }
            else if (current is ';')
            {
                Add(statements, text[start..at]);
                start = ++at;
            }
            else
            {
                at++;
            }
        }

        Add(statements, text[start..]);

        return statements;
    }

    private static void Add(List<string> statements, string statement)
    {
        string trimmed = statement.Trim();

        if (trimmed.Length is not 0)
        {
            statements.Add(trimmed);
        }
    }

    // The indentation every line that holds anything shares, taken off each line.
    private static string Dedented(string text)
    {
        string[] lines = text.Split('\n');
        int shared = lines
            .Where(line => line.Trim().Length is not 0)
            .Select(line => line.Length - line.TrimStart(' ').Length)
            .DefaultIfEmpty(0)
            .Min();

        return string.Join('\n', lines.Select(line => line.Length >= shared ? line[shared..] : line.TrimStart(' ')));
    }

    // Past a literal or an identifier quoted by the mark at the start, where a doubled
    // mark stands for itself.
    private static int Closing(string text, int start, char mark)
    {
        int at = start + 1;

        while (at < text.Length)
        {
            if (text[at] == mark)
            {
                if (at + 1 < text.Length && text[at + 1] == mark)
                {
                    at += 2;
                    continue;
                }

                return at + 1;
            }

            at++;
        }

        return text.Length;
    }

    // Past a block comment, which nests.
    private static int CommentEnd(string text, int start)
    {
        int depth = 0;
        int at = start;

        while (at + 1 < text.Length)
        {
            if (text[at] is '/' && text[at + 1] is '*')
            {
                depth++;
                at += 2;
            }
            else if (text[at] is '*' && text[at + 1] is '/')
            {
                depth--;
                at += 2;

                if (depth is 0)
                {
                    return at;
                }
            }
            else
            {
                at++;
            }
        }

        return text.Length;
    }

    // A character an identifier or a tag goes on with; a dollar sign that follows one
    // opens no body.
    private static bool Continues(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '_' or '$';

    // The tag that opens a dollar-quoted body at the dollar sign, its marks included:
    // nothing, or a letter or underscore and then letters, digits and underscores,
    // between two dollar signs. A dollar sign and a digit is a parameter, not a tag.
    private static string? Tag(string text, int start)
    {
        int at = start + 1;

        if (at < text.Length && text[at] is not '$' && !(char.IsAsciiLetter(text[at]) || text[at] is '_'))
        {
            return null;
        }

        while (at < text.Length && Continues(text[at]) && text[at] is not '$')
        {
            at++;
        }

        return at < text.Length && text[at] is '$' ? text[start..(at + 1)] : null;
    }
}
