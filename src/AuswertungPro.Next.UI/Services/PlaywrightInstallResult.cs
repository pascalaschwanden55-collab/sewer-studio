namespace AuswertungPro.Next.UI;

public sealed record PlaywrightInstallResult(
    bool Success,
    int ExitCode,
    string Tool,
    string ScriptPath,
    string StdOut,
    string StdErr)
{
    public string CombinedOutput
    {
        get
        {
            var o = (StdOut ?? "").Trim();
            var e = (StdErr ?? "").Trim();
            if (string.IsNullOrWhiteSpace(o)) return e;
            if (string.IsNullOrWhiteSpace(e)) return o;
            return o + "\n" + e;
        }
    }
}
