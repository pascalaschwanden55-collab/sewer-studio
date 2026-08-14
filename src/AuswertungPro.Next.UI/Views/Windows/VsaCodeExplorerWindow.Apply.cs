using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Application.UseCases.PhotoAnnotations;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using AuswertungPro.Next.UI.Ai.Vsa;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;
using Anim = AuswertungPro.Next.UI.Controls.Animations;
using UiControls = AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Uebernehmen und Schliessen des VSA-Codierfensters — getrennt von Aufbau, Kaskade und
/// Anzeige (Gesamtaudit 2026-08-14): Die Fehlerklammer um den Speicherweg hat die
/// Hauptdatei ueber die 1000-Zeilen-Grenze gebracht, und der Fitnesstest verlangt zu
/// Recht das Teilen der Verantwortung statt eine hoehere Grenze.
/// </summary>
public partial class VsaCodeExplorerWindow
{

    /// <summary>
    /// Uebernimmt die Beobachtung und schliesst das Fenster.
    ///
    /// Bewusst <c>Task</c> statt <c>void</c> (Gesamtaudit 2026-08-14, Prio 2): Eine
    /// Ausnahme nach dem ersten <c>await</c> waere aus einer <c>async void</c>-Methode
    /// direkt an die WPF-Oberflaeche gelangt und haette das Programm beendet. Die
    /// Aufrufer gehen ueber <see cref="StartApplyAndClose"/>, das den Fehler anzeigt.
    /// Die Fortsetzungen bleiben ohne ConfigureAwait auf dem UI-Thread.
    /// </summary>
    private async Task ApplyAndCloseAsync()
    {
        if (!_vm.CanConfirm || _photoAnnotationSaveInProgress)
            return;

        var selectedPreview = _vm.BuildProtocolEntryPreview();
        var useFrozenPreview = false;
        if (_photoAnnotations is not null && _pendingPhotoAnnotations.Count > 0)
        {
            // Ab hier gehoeren Code, Meter und alle Masken zu genau einem
            // unveraenderlichen Paket. Die UI wird vor dem ersten await gesperrt.
            var pendingBatch = _pendingPhotoAnnotations
                .OrderBy(pair => pair.Key)
                .ToArray();
            var count = pendingBatch.Length;
            useFrozenPreview = true;
            var confirmed = DialogHost.Current.Confirm(
                $"Die {count} sichtbare{(count == 1 ? "" : "n")} SAM-Maske"
                + $"{(count == 1 ? "" : "n")} mit dem finalen Code "
                + $"'{selectedPreview.Code}' als persoenliches KI-Goldbeispiel speichern?\n\n"
                + "Ja = Originalbild, Box, Maske und Code in Goldbestand und KB speichern.\n"
                + "Nein = Beobachtung ohne KI-Lernen uebernehmen.",
                "KI-Beispiel bestaetigen");

            if (!confirmed)
            {
                MarkPhotoAnnotationHandled(
                    selectedPreview,
                    sampleIds: [],
                    "Fotoannotation wurde bewusst nicht als KI-Beispiel freigegeben.");
                _pendingPhotoAnnotations.Clear();
            }
            else
            {
                _photoAnnotationSaveInProgress = true;
                _photoAnnotationSaveCancellation = new CancellationTokenSource();
                RootContent.IsEnabled = false;
                BtnApply.IsEnabled = false;
                BtnCancel.IsEnabled = false;
                PhotoAnnotationBatchSaveResult batchResult;
                try
                {
                    batchResult = await PhotoAnnotationBatchSaveUseCase.ExecuteAsync(
                        _photoAnnotations,
                        new PhotoAnnotationBatchSaveRequest(
                            pendingBatch
                                .Select(pair => new PhotoAnnotationBatchItem(
                                    pair.Key,
                                    pair.Value))
                                .ToArray(),
                            selectedPreview,
                            Environment.UserName),
                        _photoAnnotationSaveCancellation.Token);
                }
                finally
                {
                    _photoAnnotationSaveCancellation.Dispose();
                    _photoAnnotationSaveCancellation = null;
                    _photoAnnotationSaveInProgress = false;
                    RootContent.IsEnabled = true;
                    BtnApply.IsEnabled = _vm.CanConfirm;
                    BtnCancel.IsEnabled = true;
                }

                foreach (var savedPhotoIndex in batchResult.SavedPhotoIndices)
                    _pendingPhotoAnnotations.Remove(savedPhotoIndex);

                if (batchResult.FailureMessage is not null)
                {
                    if (batchResult.SavedCount == 0)
                    {
                        DialogHost.Current.Warn(
                            batchResult.FailureMessage
                            + "\n\nDie Beobachtung bleibt offen. "
                            + "Bitte die Markierung pruefen oder ohne KI-Lernen uebernehmen.",
                            "KI-Beispiel nicht gespeichert");
                        return;
                    }

                    // Ein bereits geschriebenes Goldsample ist nicht rueckholbar.
                    // Darum wird jetzt genau der vorher eingefrorene Eintrag
                    // uebernommen; so kann er weder abgebrochen noch umcodiert werden.
                    MarkPhotoAnnotationHandled(
                        selectedPreview,
                        batchResult.SampleIds,
                        "Mindestens eine Fotoannotation wurde separat gespeichert; "
                        + "weitere Fotoannotationen konnten nicht gespeichert werden.");
                    DialogHost.Current.Warn(
                        batchResult.FailureMessage
                        + $"\n\n{batchResult.SavedCount} "
                        + (batchResult.SavedCount == 1
                            ? "KI-Beispiel wurde"
                            : "KI-Beispiele wurden")
                        + " bereits sicher gespeichert. "
                        + "Die Beobachtung wird deshalb unveraendert mit dem angezeigten Code uebernommen.",
                        "KI-Beispiel teilweise gespeichert");
                }
                else
                {
                    MarkPhotoAnnotationHandled(
                        selectedPreview,
                        batchResult.SampleIds,
                        "Fotoannotation wurde separat ueber den geschuetzten Gold-/KB-Weg gespeichert.");
                }

                if (batchResult.Warnings.Count > 0)
                {
                    DialogHost.Current.Warn(
                        string.Join(
                            Environment.NewLine,
                            batchResult.Warnings.Distinct()),
                        "KI-Beispiel mit Hinweis gespeichert");
                }
            }
        }

        SelectedEntry = useFrozenPreview
            ? selectedPreview
            : _vm.BuildProtocolEntry();

        // Nutzung zaehlen -> naechstes Mal als Favoriten-Chip verfuegbar.
        _codeUsage.Erfasse(_vm.FinalCode);

        DialogResult = true;
        Close();
    }

    /// <summary>
    /// Startet die Uebernahme und faengt jeden Fehler ab. Ohne diese Klammer wuerde eine
    /// Ausnahme nach dem ersten await unbehandelt an die Oberflaeche gehen.
    /// </summary>
    private void StartApplyAndClose()
    {
        _ = ApplyAndCloseGuardedAsync();
    }

    private async Task ApplyAndCloseGuardedAsync()
    {
        try
        {
            await ApplyAndCloseAsync();
        }
        catch (OperationCanceledException)
        {
            // Ein bewusster Abbruch ist kein Fehler.
        }
        catch (Exception ex)
        {
            var meldung = Application.Common.UserError.DescribeAndReport(
                ex,
                "Beobachtung uebernehmen");
            DialogHost.Current.Warn(
                $"Die Beobachtung konnte nicht uebernommen werden:\n{meldung}",
                "VSA-Codierung");
        }
    }

    private static void MarkPhotoAnnotationHandled(
        ProtocolEntry entry,
        IEnumerable<string> sampleIds,
        string reason)
    {
        entry.Training = new ProtocolEntryTrainingMeta
        {
            SkipAutomaticPersistence = true,
            SkipReason = reason,
            PhotoAnnotationSampleIds = sampleIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList()
        };
    }
}
