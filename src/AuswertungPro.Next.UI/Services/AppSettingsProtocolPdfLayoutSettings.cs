using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Liest die eingestellte Anzahl Fotos je Seite aus den Programmeinstellungen.
/// Der Wert wird bei jedem Zugriff neu gelesen, damit eine Aenderung ohne Programmneustart
/// beim naechsten erzeugten PDF wirkt. Ein unbekannter oder unlesbarer Wert darf den
/// Export nie stoppen und faellt auf den bisherigen Stand zurueck.
/// </summary>
public sealed class AppSettingsProtocolPdfLayoutSettings : IProtocolPdfLayoutSettings
{
    private readonly Func<int?> _read;

    public AppSettingsProtocolPdfLayoutSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _read = () => settings.ProtocolPhotosPerPage;
    }

    public AppSettingsProtocolPdfLayoutSettings(Func<int?> read)
        => _read = read ?? throw new ArgumentNullException(nameof(read));

    public int PhotosPerPage => ProtocolPdfPhotoLayout.Normalize(SafeRead());

    private int? SafeRead()
    {
        try
        {
            return _read();
        }
        catch
        {
            return null;
        }
    }

}
