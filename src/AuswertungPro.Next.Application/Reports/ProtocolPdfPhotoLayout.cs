namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Feste Anordnung der Fotos auf einer Fotoseite des Haltungsprotokolls.
/// Die Masse stammen bewusst aus einer Tabelle und nicht aus einer Formel: so ist fuer
/// jeden erlaubten Wert pruefbar, dass die Fotos auf ein A4-Blatt passen.
/// </summary>
public readonly record struct ProtocolPdfPhotoLayout(
    int PhotosPerPage,
    int Columns,
    int Rows,
    float PhotoWidth,
    float PhotoHeight)
{
    /// <summary>Nutzbare Hoehe einer Fotoseite: A4 abzueglich Raender, Kopf-/Fusszeile, Titel und Kopftabelle.</summary>
    public const float AvailableHeight = 660f;

    /// <summary>Nutzbare Breite einer Fotoseite: A4 abzueglich der beiden Seitenraender.</summary>
    public const float AvailableWidth = 545f;

    /// <summary>Hoehe der Bildunterschrift unter jedem Foto.</summary>
    public const float CaptionHeight = 36f;

    /// <summary>Anzahl Fotos je Seite, wenn nichts eingestellt ist.</summary>
    public const int DefaultPhotosPerPage = 2;

    /// <summary>Die in den Einstellungen anwaehlbaren Werte, aufsteigend.</summary>
    public static IReadOnlyList<int> AllowedValues { get; } = new[] { 1, 2, 4, 6 };

    /// <summary>
    /// Loest die eingestellte Anzahl in eine Anordnung auf. Ein fehlender oder unbekannter
    /// Wert faellt still auf den bisherigen Stand mit zwei Fotos je Seite zurueck.
    /// </summary>
    public static ProtocolPdfPhotoLayout Resolve(int? photosPerPage) => photosPerPage switch
    {
        1 => new ProtocolPdfPhotoLayout(1, Columns: 1, Rows: 1, PhotoWidth: 500f, PhotoHeight: 470f),
        4 => new ProtocolPdfPhotoLayout(4, Columns: 2, Rows: 2, PhotoWidth: 265f, PhotoHeight: 250f),
        6 => new ProtocolPdfPhotoLayout(6, Columns: 2, Rows: 3, PhotoWidth: 265f, PhotoHeight: 165f),
        _ => new ProtocolPdfPhotoLayout(2, Columns: 1, Rows: 2, PhotoWidth: 500f, PhotoHeight: 255f),
    };

    /// <summary>Begrenzt einen gespeicherten Einstellungswert auf die erlaubten Werte.</summary>
    public static int Normalize(int? photosPerPage)
        => Resolve(photosPerPage).PhotosPerPage;
}
