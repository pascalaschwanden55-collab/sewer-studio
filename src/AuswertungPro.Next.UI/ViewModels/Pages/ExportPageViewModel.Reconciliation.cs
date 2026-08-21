using System;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Export;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// "Abgleichen": In Haltungen_Verteilt und Schächte_Verteilt soll nur liegen, wozu es
/// im Projekt eine Haltung bzw. einen Schacht gibt. Alles andere wandert in den
/// Papierkorb des Projekts.
///
/// Bewusst zweistufig wie der XTF-Revisions-Export: erst zeigen, was bewegt wuerde,
/// dann erst bewegen. Verschoben wird immer, geloescht nie.
/// </summary>
public sealed partial class ExportPageViewModel
{
    private async Task AbgleichenAsync()
    {
        var projektOrdner = _shell.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projektOrdner))
        {
            _dialogs.Warn("Es ist kein Projekt geoeffnet.", "Abgleichen");
            return;
        }

        var plan = await Task.Run(
            () => _distributionReconciliation.Plan(projektOrdner!, _shell.Project));

        if (!string.IsNullOrWhiteSpace(plan.BlockedReason))
        {
            _dialogs.Warn(plan.BlockedReason!, "Abgleichen");
            LastResult = plan.BlockedReason!;
            return;
        }

        if (plan.ToMove.Count == 0)
        {
            var sauber = "Abgleich: In den Verteilordnern liegt nichts ohne Gegenstueck im Projekt.";
            _dialogs.Info(
                sauber + (plan.Skipped.Count > 0
                    ? Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, plan.Skipped)
                    : string.Empty),
                "Abgleichen");
            LastResult = sauber;
            return;
        }

        if (!_dialogs.ConfirmWarn(BuildPlanText(plan), "Abgleichen", defaultNo: true))
        {
            LastResult = "Abgleich abgebrochen - es wurde nichts verschoben.";
            return;
        }

        var ergebnis = await Task.Run(
            () => _distributionReconciliation.Apply(projektOrdner!, plan, DateTime.Now));

        var bewegt = ergebnis.MovedDirectories + ergebnis.MovedFiles;
        var text = bewegt == 0
            ? "Abgleich: Es wurde nichts verschoben."
            : $"Abgleich: {ergebnis.MovedDirectories} Ordner und {ergebnis.MovedFiles} Datei(en) "
              + $"nach {ergebnis.TrashFolderRelative} verschoben.";

        LastResult = text;
        _dialogs.Info(
            text + (ergebnis.Messages.Count > 0
                ? Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, ergebnis.Messages)
                : string.Empty),
            "Abgleichen");
    }

    /// <summary>
    /// Zeigt vor dem Verschieben, was betroffen ist. Bei vielen Eintraegen nur die
    /// ersten - eine Wand aus Pfaden liest niemand, und die Zahl steht oben.
    /// </summary>
    private static string BuildPlanText(DistributionReconciliationPlan plan)
    {
        const int sichtbar = 25;
        var ordner = plan.ToMove.Count(e => e.IsDirectory);
        var dateien = plan.ToMove.Count - ordner;

        var text = $"Im Projekt gibt es zu {ordner} Ordner(n) und {dateien} Datei(en) in den "
                   + "Verteilordnern keine Haltung und keinen Schacht." + Environment.NewLine
                   + "Sie werden in den Papierkorb des Projekts verschoben, nicht geloescht."
                   + Environment.NewLine + Environment.NewLine
                   + string.Join(
                       Environment.NewLine,
                       plan.ToMove.Take(sichtbar).Select(e => "  " + e.RelativePath));

        if (plan.ToMove.Count > sichtbar)
            text += Environment.NewLine + $"  ... und {plan.ToMove.Count - sichtbar} weitere.";

        if (plan.Skipped.Count > 0)
        {
            text += Environment.NewLine + Environment.NewLine
                 + "Nicht angefasst:" + Environment.NewLine
                 + string.Join(Environment.NewLine, plan.Skipped.Select(s => "  " + s));
        }

        return text + Environment.NewLine + Environment.NewLine + "Jetzt verschieben?";
    }
}
