using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class RecordDetailHighlightPolicyTests
{
    [Theory]
    [InlineData("Sanieren_JaNein", RecordDetailHighlightKind.Sanieren)]
    [InlineData("Sanieren Ja/Nein", RecordDetailHighlightKind.Sanieren)]
    [InlineData("Ja/Nein", RecordDetailHighlightKind.Sanieren)]
    [InlineData("Ausgefuehrt_durch", RecordDetailHighlightKind.AusgefuehrtDurch)]
    [InlineData("Ausgeführt durch", RecordDetailHighlightKind.AusgefuehrtDurch)]
    [InlineData("Sanieren durch", RecordDetailHighlightKind.AusgefuehrtDurch)]
    [InlineData("Bemerkungen", RecordDetailHighlightKind.None)]
    public void Resolve_erkennt_sanierungsfelder(string fieldName, RecordDetailHighlightKind expected)
        => Assert.Equal(expected, RecordDetailHighlightPolicy.Resolve(fieldName));
}
