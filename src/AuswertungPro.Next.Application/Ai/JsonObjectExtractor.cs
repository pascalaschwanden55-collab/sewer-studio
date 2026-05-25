namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Extrahiert das erste vollstaendige JSON-Objekt aus einem Roh-LLM-Text.
/// Brace-Counter respektiert Strings und Escape-Sequenzen — non-greedy Regex
/// kann verschachtelte JSONs nicht korrekt schneiden.
/// </summary>
public static class JsonObjectExtractor
{
    public static string? TryExtractFirstObject(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return null;

        var start = raw.IndexOf('{');
        if (start < 0)
            return null;

        var depth = 0;
        var inString = false;
        var escape = false;

        for (var i = start; i < raw.Length; i++)
        {
            var ch = raw[i];

            if (inString)
            {
                if (escape)
                {
                    escape = false;
                }
                else if (ch == '\\')
                {
                    escape = true;
                }
                else if (ch == '"')
                {
                    inString = false;
                }
                continue;
            }

            switch (ch)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                        return raw.Substring(start, i - start + 1);
                    break;
            }
        }

        return null;
    }
}
