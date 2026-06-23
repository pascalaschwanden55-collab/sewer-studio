using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// Laedt bestehende ProtocolEntry-Eintraege aus HaltungRecord in die Import-Referenz-Liste.
    /// KI-Befunde-Liste bleibt leer (KI erkennt frisch).
    /// </summary>
    private void LoadExistingProtocolEventsAsImport()
    {
        if (_haltungRecord?.Protocol?.Current?.Entries == null) return;

        CodingProtocolEventCollectionAppender.Append(
            _codingImportEvents,
            CodingProtocolEventMapper.BuildMissingImportEvents(
                _haltungRecord.Protocol,
                _codingImportEvents));

        RunImportDefectCount.Text = _codingImportEvents.Count.ToString();
    }
}
