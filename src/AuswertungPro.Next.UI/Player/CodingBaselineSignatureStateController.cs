namespace AuswertungPro.Next.UI.Player;

public sealed class CodingBaselineSignatureStateController
{
    public string BaselineSignature { get; private set; } = string.Empty;

    public void Set(string baselineSignature)
        => BaselineSignature = baselineSignature;
}
