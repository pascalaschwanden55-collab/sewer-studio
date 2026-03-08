using System.Globalization;

namespace AuswertungPro.Next.Infrastructure.Devis;

/// <summary>
/// Simple expression evaluator for quantity formulas.
/// Supports variable substitution and basic arithmetic (+, -, *, /).
/// </summary>
public static class FormelEvaluator
{
    public static decimal Evaluate(string formel, Dictionary<string, decimal> variablen)
    {
        var expr = formel;
        // Sort by key length descending to avoid partial replacements
        foreach (var kv in variablen.OrderByDescending(x => x.Key.Length))
        {
            expr = expr.Replace(kv.Key, kv.Value.ToString(CultureInfo.InvariantCulture));
        }

        return EvaluateExpression(expr.Trim());
    }

    private static decimal EvaluateExpression(string expr)
    {
        // Remove outer whitespace
        expr = expr.Trim();

        // Handle parentheses
        while (expr.Contains('('))
        {
            var close = expr.IndexOf(')');
            if (close < 0) break;
            var open = expr.LastIndexOf('(', close);
            if (open < 0) break;
            var inner = expr.Substring(open + 1, close - open - 1);
            var innerResult = EvaluateExpression(inner);
            expr = string.Concat(expr.AsSpan(0, open), innerResult.ToString(CultureInfo.InvariantCulture), expr.AsSpan(close + 1));
        }

        // Tokenize: split by +/- (additive), then by */÷ (multiplicative)
        return ParseAddSub(expr);
    }

    private static decimal ParseAddSub(string expr)
    {
        // Split on + and - at top level (not inside numbers)
        var terms = new List<(decimal value, char op)>();
        var current = "";
        var op = '+';

        for (int i = 0; i < expr.Length; i++)
        {
            var c = expr[i];
            if ((c == '+' || c == '-') && i > 0 && current.Trim().Length > 0)
            {
                terms.Add((ParseMulDiv(current.Trim()), op));
                current = "";
                op = c;
            }
            else
            {
                current += c;
            }
        }
        if (current.Trim().Length > 0)
            terms.Add((ParseMulDiv(current.Trim()), op));

        decimal result = 0;
        foreach (var (value, termOp) in terms)
        {
            result = termOp == '-' ? result - value : result + value;
        }
        return result;
    }

    private static decimal ParseMulDiv(string expr)
    {
        var parts = new List<(decimal value, char op)>();
        var current = "";
        var op = '*';

        for (int i = 0; i < expr.Length; i++)
        {
            var c = expr[i];
            if (c == '*' || c == '/')
            {
                if (current.Trim().Length > 0)
                    parts.Add((ParseNumber(current.Trim()), op));
                current = "";
                op = c;
            }
            else
            {
                current += c;
            }
        }
        if (current.Trim().Length > 0)
            parts.Add((ParseNumber(current.Trim()), op));

        if (parts.Count == 0)
            return 0;

        decimal result = parts[0].value;
        for (int i = 1; i < parts.Count; i++)
        {
            if (parts[i].op == '/')
            {
                if (parts[i].value == 0) continue;
                result /= parts[i].value;
            }
            else
            {
                result *= parts[i].value;
            }
        }
        return result;
    }

    private static decimal ParseNumber(string s)
    {
        if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return d;
        return 0;
    }
}
