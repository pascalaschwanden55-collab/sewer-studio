using System;
using System.Collections.Generic;
using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// AP-2 (Audit 2026-08-10): Keine eigene Einstellungsinstanz in Bedienelementen
/// und Fenstern. AppSettings.Save() schreibt immer das GANZE Objekt — wer eine
/// eigene Momentaufnahme laedt und spaeter speichert, loescht jede Aenderung,
/// die seit dem Laden an anderer Stelle geschah. Die eine Live-Instanz wird in
/// App.xaml.cs per Configure bereitgestellt. Ein Kommentar im Code hat diesen
/// Fall nicht verhindert — diese Sperre schon.
/// </summary>
public sealed class AppSettingsLoadArchitectureTests
{
    [Fact]
    public void Kein_AppSettings_Load_unter_Controls_und_Views()
    {
        var uiRoot = RepoFile("src", "AuswertungPro.Next.UI");
        var treffer = new List<string>();
        foreach (var bereich in new[] { "Controls", "Views" })
        {
            var wurzel = Path.Combine(uiRoot, bereich);
            if (!Directory.Exists(wurzel))
                continue;
            foreach (var datei in Directory.EnumerateFiles(wurzel, "*.cs", SearchOption.AllDirectories))
            {
                if (File.ReadAllText(datei).Contains("AppSettings.Load(", StringComparison.Ordinal))
                    treffer.Add(Path.GetRelativePath(uiRoot, datei));
            }
        }

        // Bekannte Ausnahme, bewusst einzeln genannt statt kommentarlos weichgezogen:
        // PlayerWindow.xaml.cs hat einen Fallback `_protocolContext.Settings ??
        // AppSettings.Load()`, und der HAT einen Schreibweg: Lautstaerke, Stumm
        // und Overlay-Deckkraft speichern ueber IPlayerControlSettingsStore.Save()
        // — genau die Momentaufnahme-Problematik dieses Pakets. Heute harmlos,
        // weil die Dependencies die Live-Instanz tragen und der Rückfall praktisch
        // nie feuert; riskant bleibt er. Der Umbau auf eine klare Quelle ist ein
        // eigener Punkt, nicht Teil von AP-2. Jeder weitere Eintrag hier waere ein
        // Rueckschritt.
        Assert.Equal(
            new[] { Path.Combine("Views", "Windows", "PlayerWindow.xaml.cs") },
            treffer);
    }
}
