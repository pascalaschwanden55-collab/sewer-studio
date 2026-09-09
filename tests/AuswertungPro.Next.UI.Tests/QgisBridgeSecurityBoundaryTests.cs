using System.IO;
using AuswertungPro.Next.UI.QgisBridge;
using Xunit;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class QgisBridgeSecurityBoundaryTests
{
    [Fact]
    public void Bridge_is_documented_and_guarded_as_local_single_user_feed()
    {
        var bridgeServer = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "QgisBridge", "QgisBridgeServer.cs"));
        var liveControl = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "LiveControl", "LiveControlServer.cs"));
        var readme = File.ReadAllText(
            RepoFile("integrations", "qgis", "README.md"));

        Assert.Contains("new TcpListener(IPAddress.Loopback", bridgeServer, StringComparison.Ordinal);
        Assert.Contains("method is not (\"GET\" or \"HEAD\" or \"POST\")", bridgeServer, StringComparison.Ordinal);
        Assert.Contains("request.Method is \"GET\" or \"POST\"", liveControl, StringComparison.Ordinal);

        Assert.Contains("Einzelplatz", readme, StringComparison.Ordinal);
        Assert.Contains("Mehrbenutzer", readme, StringComparison.Ordinal);
    }

    /// <summary>
    /// Seit dem Rueckweg aus der Karte ist die Bruecke nicht mehr rein lesend. Sie
    /// darf aber genau EINE Sache schreiben duerfen: im offenen Video an eine andere
    /// Stelle springen. Dieser Waechter haelt diese Grenze fest — ein zweiter
    /// schreibender Pfad muss hier auffallen, nicht erst im Betrieb.
    /// </summary>
    [Fact]
    public void Der_schreibende_Weg_kennt_genau_einen_Pfad()
    {
        var router = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "QgisBridge", "QgisBridgeEndpointRouter.cs"));

        // Genau ein Fall im schreibenden Router, alles andere ist 404.
        var schreibend = router[router.IndexOf("public QgisBridgeResponse RoutePost", StringComparison.Ordinal)..];
        var faelle = schreibend.Split("\"/qgis/", StringSplitOptions.None).Length - 1;
        Assert.Equal(1, faelle);
        Assert.Contains("\"/qgis/seek\" => Seek(body)", schreibend, StringComparison.Ordinal);
        Assert.Contains("_ => Error(404", schreibend, StringComparison.Ordinal);
    }

    /// <summary>
    /// Der Rumpf wird vor der Anmeldung gelesen — deshalb braucht er eine harte
    /// Grenze, sonst bindet ein defekter oder boesartiger lokaler Prozess Speicher.
    /// </summary>
    [Fact]
    public void Der_Rumpf_einer_Schreibanfrage_ist_begrenzt()
    {
        var bridgeServer = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "QgisBridge", "QgisBridgeServer.cs"));

        Assert.Contains("MaxBodyBytes", bridgeServer, StringComparison.Ordinal);
        Assert.Contains("ReadBodyAsync(contentLength, MaxBodyBytes", bridgeServer, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gesamtaudit 2026-08-14, P1-3: Beide Wege zu den /qgis-Daten verlangen einen Token.
    /// Der zweite Weg (Live-Control auf demselben Port) war vorher ausdruecklich offen.
    /// </summary>
    [Fact]
    public void Beide_Wege_zu_den_Qgis_Daten_verlangen_einen_Token()
    {
        var bridgeServer = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "QgisBridge", "QgisBridgeServer.cs"));
        var liveControl = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "LiveControl", "LiveControlServer.cs"));

        Assert.Contains("QgisBridgeToken.Matches(_token, token)", bridgeServer, StringComparison.Ordinal);
        Assert.Contains("401", bridgeServer, StringComparison.Ordinal);

        Assert.Contains("QgisBridgeToken.Matches(_qgisToken, request.QgisToken)", liveControl, StringComparison.Ordinal);
        Assert.Contains("if (!qgisErlaubt)", liveControl, StringComparison.Ordinal);
    }

    [Fact]
    public void Interne_Fehlertexte_verlassen_die_Bruecke_nicht()
    {
        var processor = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "QgisBridge", "QgisBridgeRequestProcessor.cs"));

        // Der Ausnahmetext darf nicht mehr als Antwort hinausgehen.
        Assert.DoesNotContain("Error(500, ex.Message)", processor, StringComparison.Ordinal);
        Assert.Contains("Einzelheiten stehen im SewerStudio-Protokoll", processor, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Readme_erklaert_woher_der_Token_kommt()
    {
        var readme = File.ReadAllText(RepoFile("integrations", "qgis", "README.md"));

        Assert.Contains(QgisBridgeToken.FileName, readme, StringComparison.Ordinal);
        Assert.Contains(QgisBridgeToken.EnvVarName, readme, StringComparison.Ordinal);
        Assert.Contains(QgisBridgeToken.HeaderName, readme, StringComparison.Ordinal);
    }
}

public sealed class QgisBridgeTokenTests
{
    [Fact]
    public void Ein_fehlender_oder_falscher_Token_passt_nie()
    {
        Assert.False(QgisBridgeToken.Matches("erwartet", null));
        Assert.False(QgisBridgeToken.Matches("erwartet", ""));
        Assert.False(QgisBridgeToken.Matches("erwartet", "falsch"));
        // Praefix und Verlaengerung gelten ebenfalls nicht als Treffer
        Assert.False(QgisBridgeToken.Matches("erwartet", "erwarte"));
        Assert.False(QgisBridgeToken.Matches("erwartet", "erwartetX"));
    }

    [Fact]
    public void Der_richtige_Token_passt()
        => Assert.True(QgisBridgeToken.Matches("abc123", "abc123"));

    [Fact]
    public void Ein_leerer_erwarteter_Token_oeffnet_keine_Tuer()
    {
        // Sonst wuerde ein Fehler beim Erzeugen des Tokens die Anmeldung aushebeln.
        Assert.False(QgisBridgeToken.Matches(null, null));
        Assert.False(QgisBridgeToken.Matches("", ""));
        Assert.False(QgisBridgeToken.Matches("", "irgendwas"));
    }

    [Fact]
    public void Die_Tokendatei_liegt_im_AppData_Ordner_der_App()
    {
        var pfad = QgisBridgeToken.TokenFilePath;

        Assert.EndsWith(QgisBridgeToken.FileName, pfad, StringComparison.Ordinal);
        Assert.True(Path.IsPathRooted(pfad));
    }
}
