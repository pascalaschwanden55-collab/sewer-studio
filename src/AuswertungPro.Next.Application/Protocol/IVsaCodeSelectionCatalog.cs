using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Application.Protocol;

public interface IVsaCodeSelectionCatalog
{
    IReadOnlyDictionary<string, GroupDef> Groups { get; }
    (QuantField? Q1, QuantField? Q2) GetQuantRule(string codeKey, string? char1Key);
    ClockRule GetClockRule(string codeKey);
    IReadOnlyDictionary<string, string>? GetChar2Options(VsaCodeDef codeDef, string char1Key);
    bool IsInvalidCombo(VsaCodeDef codeDef, string char1Key, string char2Key);
}

public sealed class VsaCodeTreeSelectionCatalog : IVsaCodeSelectionCatalog
{
    public IReadOnlyDictionary<string, GroupDef> Groups => VsaCodeTree.Groups;

    public (QuantField? Q1, QuantField? Q2) GetQuantRule(string codeKey, string? char1Key)
        => VsaCodeTree.GetQuantRule(codeKey, char1Key);

    public ClockRule GetClockRule(string codeKey)
        => VsaCodeTree.GetClockRule(codeKey);

    public IReadOnlyDictionary<string, string>? GetChar2Options(VsaCodeDef codeDef, string char1Key)
        => VsaCodeTree.GetChar2Options(codeDef, char1Key);

    public bool IsInvalidCombo(VsaCodeDef codeDef, string char1Key, string char2Key)
        => VsaCodeTree.IsInvalidCombo(codeDef, char1Key, char2Key);
}
