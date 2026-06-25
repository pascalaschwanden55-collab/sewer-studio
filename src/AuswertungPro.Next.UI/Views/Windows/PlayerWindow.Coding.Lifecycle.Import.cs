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
        CodingExistingProtocolImportEventsWorkflow.Execute(
            new CodingExistingProtocolImportEventsWorkflowRequest(
                _haltungRecord?.Protocol,
                _codingImportEvents),
            new CodingExistingProtocolImportEventsWorkflowActions(
                SetImportCount: count => CodingImportReferenceControls.SetCount(
                    RunImportDefectCount,
                    count)));
    }
}
