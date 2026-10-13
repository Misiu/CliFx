using System.Collections.Generic;
using System.Text;

namespace CliFx.Completion;

/// <summary>
/// Splits raw command line text (as typed by the user in a shell) into individual words.
/// </summary>
internal static class CommandLineTokenizer
{
    private static bool IsEscapable(char c) => char.IsWhiteSpace(c) || c is '"' or '\'';

    public static IReadOnlyList<string> Split(string text, out bool isLastWordUnfinished)
    {
        var words = new List<string>();
        var buffer = new StringBuilder();

        var isInsideWord = false;
        var quote = default(char?);

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            // Escaped character (backslash for POSIX shells, backtick for PowerShell)
            if (
                c is '\\' or '`'
                && quote is null
                && i + 1 < text.Length
                && IsEscapable(text[i + 1])
            )
            {
                buffer.Append(text[++i]);
                isInsideWord = true;
            }
            // Closing quote
            else if (quote is not null && c == quote)
            {
                quote = null;
            }
            // Inside quotes everything is taken literally
            else if (quote is not null)
            {
                buffer.Append(c);
            }
            // Opening quote
            else if (c is '"' or '\'')
            {
                quote = c;
                isInsideWord = true;
            }
            // Word separator
            else if (char.IsWhiteSpace(c))
            {
                if (isInsideWord)
                {
                    words.Add(buffer.ToString());
                    buffer.Clear();
                    isInsideWord = false;
                }
            }
            else
            {
                buffer.Append(c);
                isInsideWord = true;
            }
        }

        if (isInsideWord)
            words.Add(buffer.ToString());

        isLastWordUnfinished = isInsideWord;

        return words;
    }
}
