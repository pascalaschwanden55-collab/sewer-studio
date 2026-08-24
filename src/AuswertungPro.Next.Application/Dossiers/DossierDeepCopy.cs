using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Die Sicherungskopie einer Dossierangabe vor einer Bearbeitung.
///
/// Bearbeitungsfenster aendern die uebergebenen Angaben unmittelbar. Wer
/// danach zurueckrollen will — weil das Speichern misslang oder der Benutzer
/// verworfen hat — braucht einen vollstaendigen Vorherstand. Ein halbtiefer
/// Klon wuerde die Zeilenlisten teilen: das Zuruecksetzen bliebe wirkungslos
/// und der Bildschirm zeigte Angaben, die nicht auf der Platte stehen.
///
/// Der Weg ueber JSON ist bewusst gewaehlt: die Dossierangaben sind reine
/// Datenklassen und werden ohnehin so gespeichert. Was die Datei ueberlebt,
/// ueberlebt auch die Kopie — eine handgeschriebene Klonmethode muesste bei
/// jedem neuen Feld nachgezogen werden und wuerde es irgendwann vergessen.
/// </summary>
public static class DossierDeepCopy
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static T Of<T>(T source) where T : new()
    {
        ArgumentNullException.ThrowIfNull(source);

        var json = JsonSerializer.Serialize(source, Options);
        return JsonSerializer.Deserialize<T>(json, Options) ?? new T();
    }
}
