using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Dossiers.Preview;

/// <summary>
/// Fachliche Adresse einer anklickbaren Stelle in der Dossier-Vorschau.
/// Sie haengt an Feld, Tabellenzeile und Spalte — niemals an Pixelkoordinaten.
/// </summary>
public enum DossierPreviewTargetKind
{
    Field,
    Literal,
    Row,
    RowCell
}

/// <summary>
/// Dieselbe Adresse wird vom Blatt und vom Editor verwendet. Dadurch kann ein
/// Klick auch in einer dynamisch gewachsenen Tabelle genau zum passenden Feld
/// springen.
/// </summary>
public readonly record struct DossierPreviewTarget(
    DossierPreviewTargetKind Kind,
    string Key,
    int RowIndex = -1,
    string CellKey = "")
{
    public static DossierPreviewTarget Field(string key)
        => Create(DossierPreviewTargetKind.Field, key);

    public static DossierPreviewTarget Literal(string originalText)
        => Create(DossierPreviewTargetKind.Literal, originalText);

    public static DossierPreviewTarget Row(string key, int rowIndex)
        => Create(DossierPreviewTargetKind.Row, key, rowIndex);

    public static DossierPreviewTarget RowCell(string key, int rowIndex, string cellKey)
        => Create(DossierPreviewTargetKind.RowCell, key, rowIndex, cellKey);

    /// <summary>
    /// Waehlt unter allen Marken eines gezeichneten Elements die genaueste,
    /// fuer die rechts wirklich ein Editor vorhanden ist.
    /// </summary>
    public static DossierPreviewTarget? SelectMostSpecific(
        IEnumerable<DossierPreviewTarget> targets,
        Func<DossierPreviewTarget, bool> isAvailable)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(isAvailable);

        foreach (var target in targets
            .Where(isAvailable)
            .OrderByDescending(target => target.Specificity))
        {
            return target;
        }

        return null;
    }

    private int Specificity => Kind switch
    {
        DossierPreviewTargetKind.RowCell => 4,
        DossierPreviewTargetKind.Row => 3,
        DossierPreviewTargetKind.Field or DossierPreviewTargetKind.Literal => 2,
        _ => 0
    };

    private static DossierPreviewTarget Create(
        DossierPreviewTargetKind kind,
        string key,
        int rowIndex = -1,
        string cellKey = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (kind is DossierPreviewTargetKind.Row or DossierPreviewTargetKind.RowCell
            && rowIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowIndex));
        }

        if (kind is DossierPreviewTargetKind.RowCell)
            ArgumentException.ThrowIfNullOrWhiteSpace(cellKey);

        return new DossierPreviewTarget(kind, key, rowIndex, cellKey);
    }
}
