using System.Security.Cryptography;
using System.Text;

namespace AuswertungPro.Next.Application.Diagnostics;

/// <summary>
/// Erzeugt reproduzierbare Fehlercodes aus (Area + Key). In Dev-Phase nützlich für schnelle Fehlersuche.
/// Später kann die Anzeige per Settings deaktiviert werden.
/// </summary>
public sealed class ErrorCodeGenerator
{
    public string Generate(string area, string key)
    {
        // area: UI|IMP|EXP|VSA|APP  (Kurz)
        // key:  freie Kennung (z.B. "ImportPdf.ReadFile")
        var raw = $"{area}:{key}".Trim();
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        // 4 Bytes => 8 Hex (reicht als ID, kollisionsarm für App-Fehler)
        var id = Convert.ToHexString(hash.AsSpan(0, 4));
        return $"{area}-{id}";
    }

    public string GenerateForException(string area, Exception ex, string? context = null)
    {
        var key = ex.GetType().Name + "|" + (ex.Message ?? "") + "|" + (context ?? "");
        return Generate(area, key);
    }
}
