using System;
using System.Collections.Generic;
using System.IO;

using AuswertungPro.Next.Infrastructure.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Das Benutzerprofil, mit dem LibreOffice die Umwandlung fährt.
///
/// Bisher bekam jede Umwandlung ein frisches Profil. Gemessen auf diesem
/// Rechner: 2,35 s je Lauf. Mit EINEM wiederverwendeten Profil sind es ab dem
/// zweiten Lauf rund 1,0 s — der grösste Teil der Zeit ging in den Aufbau des
/// Profils, nicht in die Umwandlung.
///
/// Eigen bleibt das Profil trotzdem: Es liegt im Temp-Ordner und nicht im
/// Profil des Benutzers. Ein gleichzeitig geöffnetes LibreOffice wird dadurch
/// nicht gestört — das war der Grund für die Trennung und bleibt es.
/// </summary>
public sealed class LibreOfficeProfileTests
{
    [Fact]
    public void Derselbe_Lauf_verwendet_dasselbe_Profil()
    {
        Assert.Equal(LibreOfficeProfileStore.Ordner(), LibreOfficeProfileStore.Ordner());
    }

    [Fact]
    public void Das_Profil_liegt_im_Temp_Ordner_und_nicht_beim_Benutzer()
    {
        var ordner = LibreOfficeProfileStore.Ordner();

        Assert.StartsWith(
            Path.GetFullPath(Path.GetTempPath()),
            Path.GetFullPath(ordner),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ein_erneuertes_Profil_ist_ein_anderes()
    {
        // Ein beschaedigtes Profil wuerde sonst jede weitere Umwandlung kosten.
        var vorher = LibreOfficeProfileStore.Ordner();

        LibreOfficeProfileStore.Erneuere();

        Assert.NotEqual(vorher, LibreOfficeProfileStore.Ordner());
    }

    [Fact]
    public void Nach_einem_Fehlschlag_wird_mit_frischem_Profil_wiederholt()
    {
        // Genau der Fall, den die Wiederverwendung erst moeglich macht: Ein
        // altes Profil darf nicht dauerhaft alles blockieren.
        var verwendeteProfile = new List<string>();

        var erfolg = LibreOfficeWriterPdfConverter.TryConvertToPdf(
            "quelle.docx",
            "ziel.pdf",
            profil =>
            {
                verwendeteProfile.Add(profil);
                return verwendeteProfile.Count > 1;
            });

        Assert.True(erfolg);
        Assert.Equal(2, verwendeteProfile.Count);
        Assert.NotEqual(verwendeteProfile[0], verwendeteProfile[1]);
    }

    [Fact]
    public void Ein_erfolgreicher_Lauf_wiederholt_nicht()
    {
        var laeufe = 0;

        var erfolg = LibreOfficeWriterPdfConverter.TryConvertToPdf(
            "quelle.docx",
            "ziel.pdf",
            _ =>
            {
                laeufe++;
                return true;
            });

        Assert.True(erfolg);
        Assert.Equal(1, laeufe);
    }

    [Fact]
    public void Scheitern_beide_Laeufe_ist_das_Ergebnis_ehrlich_falsch()
    {
        var erfolg = LibreOfficeWriterPdfConverter.TryConvertToPdf(
            "quelle.docx",
            "ziel.pdf",
            _ => false);

        Assert.False(erfolg);
    }
}
