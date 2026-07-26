using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

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
                _protocolContext.HaltungRecord?.Protocol,
                _codingImportReferenceEvents.Events),
            new CodingExistingProtocolImportEventsWorkflowActions(
                SetImportCount: count => CodingImportReferenceControls.SetCount(
                    RunImportDefectCount,
                    count)));
    }
}
