namespace AuswertungPro.Next.UI.LiveControl;

public static class LiveControlRequestValidator
{
    public static bool IsSafeResourceKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 80)
            return false;

        return key.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.');
    }
}
