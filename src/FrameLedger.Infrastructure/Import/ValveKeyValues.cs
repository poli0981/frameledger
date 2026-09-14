using System.Text;

namespace FrameLedger.Infrastructure.Import;

/// <summary>
/// Valve's text KeyValues format (<c>libraryfolders.vdf</c>, <c>appmanifest_*.acf</c>), read into nested dictionaries:
/// <c>"key" "value"</c> pairs and <c>"key" { … }</c> blocks, quoted or bare tokens, <c>//</c> comments, the
/// escapes <c>\"</c> <c>\\</c> <c>\n</c> <c>\t</c>. Keys compare case-insensitively (Steam writes <c>AppState</c>
/// and <c>appid</c> in whatever case it likes). Malformed input yields what parsed up to the fault, never an
/// exception — a launcher file is untrusted input to this process (P4 PR-4).
/// </summary>
public static class ValveKeyValues
{
    public static KeyValuesBlock Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var root = new KeyValuesBlock();
        int pos = 0;
        ParseInto(root, text, ref pos, depth: 0);
        return root;
    }

    private static void ParseInto(KeyValuesBlock block, string text, ref int pos, int depth)
    {
        while (true)
        {
            string? key = NextToken(text, ref pos, out bool closing);
            if (key is null || closing)
            {
                return;
            }

            string? value = NextToken(text, ref pos, out bool _, out bool opening);
            if (opening)
            {
                var child = new KeyValuesBlock();
                if (depth < 32)
                {
                    ParseInto(child, text, ref pos, depth + 1);
                }

                block.Set(key, child);
            }
            else if (value is not null)
            {
                block.Set(key, value);
            }
            else
            {
                return;
            }
        }
    }

    private static string? NextToken(string text, ref int pos, out bool closing) => NextToken(text, ref pos, out closing, out _);

    private static string? NextToken(string text, ref int pos, out bool closing, out bool opening)
    {
        closing = false;
        opening = false;
        while (pos < text.Length)
        {
            char c = text[pos];
            if (char.IsWhiteSpace(c))
            {
                pos++;
            }
            else if (c == '/' && pos + 1 < text.Length && text[pos + 1] == '/')
            {
                while (pos < text.Length && text[pos] != '\n')
                {
                    pos++;
                }
            }
            else
            {
                break;
            }
        }

        if (pos >= text.Length)
        {
            return null;
        }

        switch (text[pos])
        {
            case '{':
                pos++;
                opening = true;
                return string.Empty;
            case '}':
                pos++;
                closing = true;
                return string.Empty;
            case '"':
                return Quoted(text, ref pos);
            default:
                int start = pos;
                while (pos < text.Length && !char.IsWhiteSpace(text[pos]) && text[pos] != '{' && text[pos] != '}')
                {
                    pos++;
                }

                return text[start..pos];
        }
    }

    private static string Quoted(string text, ref int pos)
    {
        var sb = new StringBuilder();
        pos++;    // the opening quote
        while (pos < text.Length)
        {
            char c = text[pos++];
            if (c == '"')
            {
                return sb.ToString();
            }

            if (c == '\\' && pos < text.Length)
            {
                char e = text[pos++];
                sb.Append(e switch
                {
                    'n' => '\n',
                    't' => '\t',
                    '"' => '"',
                    '\\' => '\\',
                    _ => e,
                });
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();    // unterminated: what was read
    }
}
