using System.IO;
using System.Text.RegularExpressions;
using Xunit;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Haelt die Verdrahtung des Anpassen-Modus fest. Die Rechenteile pruefen
/// <see cref="RecordDetailLayoutApplierTests"/> und <see cref="RecordDetailDragOperationsTests"/>;
/// hier geht es nur darum, dass sie im Programm auch wirklich aufgerufen werden — eine
/// tote Regel faellt sonst nicht auf.
/// </summary>
public sealed class RecordDetailCustomizeWiringTests
{
    private static string ReadFile(params string[] segments)
        => File.ReadAllText(RepoFile(segments));

    private static string ReadDetailsXaml()
        => ReadFile("src", "AuswertungPro.Next.UI", "Views", "Controls", "RecordDetailsView.xaml");

    /// <summary>
    /// Schneidet die Vorlage einer einzelnen Feldkarte aus. Ohne diese Eingrenzung wuerde
    /// ein Treffer irgendwo sonst in der Datei den Test gruen halten.
    /// </summary>
    private static string ExtractFieldCardTemplate(string xaml)
    {
        var marker = xaml.IndexOf("PreviewDrop=\"FieldCard_PreviewDrop\"", StringComparison.Ordinal);
        Assert.True(marker > 0, "Feldkarten-Vorlage in RecordDetailsView.xaml nicht gefunden.");

        var start = xaml.LastIndexOf("<Border", marker, StringComparison.Ordinal);
        Assert.True(start >= 0, "Border der Feldkarte nicht gefunden.");

        var end = xaml.IndexOf("ContentTemplateSelector=\"{StaticResource EditorTemplateSelector}\"", marker, StringComparison.Ordinal);
        Assert.True(end > start, "Ende der Feldkarten-Vorlage nicht gefunden.");

        return xaml[start..end];
    }

    // --- Karte --------------------------------------------------------------

    [Fact]
    public void Feldkarte_nimmt_eine_gezogene_Karte_an()
    {
        var card = ExtractFieldCardTemplate(ReadDetailsXaml());

        Assert.Contains("AllowDrop=\"True\"", card);
        Assert.Contains("PreviewDragOver=\"FieldCard_PreviewDragOver\"", card);
        Assert.Contains("PreviewDrop=\"FieldCard_PreviewDrop\"", card);
    }

    [Fact]
    public void Nur_die_Beschriftung_ist_der_Ziehgriff()
    {
        var card = ExtractFieldCardTemplate(ReadDetailsXaml());

        // Die Handler haengen am Beschriftungs-TextBlock, nicht an der ganzen Karte:
        // sonst liesse sich im Eingabefeld kein Text mehr markieren.
        var label = Regex.Match(card, @"<TextBlock Text=""\{Binding Label\}""[\s\S]*?</TextBlock>");
        Assert.True(label.Success, "Beschriftungs-TextBlock der Feldkarte nicht gefunden.");
        Assert.Contains("PreviewMouseLeftButtonDown=\"FieldHandle_PreviewMouseLeftButtonDown\"", label.Value);
        Assert.Contains("PreviewMouseMove=\"FieldHandle_PreviewMouseMove\"", label.Value);
    }

    [Fact]
    public void Die_Karte_verschwindet_aus_zwei_getrennten_Gruenden()
    {
        var card = ExtractFieldCardTemplate(ReadDetailsXaml());

        // IsVisible ist die fachliche Regel (Sanierungs-Folgefelder), IsHiddenByUser die
        // persoenliche Einstellung. Die eine darf die andere nicht ueberschreiben.
        Assert.Contains("<DataTrigger Binding=\"{Binding IsVisible}\" Value=\"False\">", card);
        Assert.Contains("<DataTrigger Binding=\"{Binding IsHiddenByUser}\" Value=\"True\">", card);
    }

    [Fact]
    public void Das_Ausblenden_Kreuz_erscheint_nur_im_Anpassen_Modus()
    {
        var card = ExtractFieldCardTemplate(ReadDetailsXaml());

        var button = Regex.Match(card, @"<Button[^>]*Click=""HideField_Click""[\s\S]*?/>");
        Assert.True(button.Success, "Ausblenden-Knopf an der Feldkarte nicht gefunden.");
        Assert.Contains("IsCustomizing", button.Value);
    }

    // --- Spalte -------------------------------------------------------------

    [Fact]
    public void Der_Spaltentitel_ist_der_Griff_und_die_Spalte_die_Ablageflaeche()
    {
        var xaml = ReadDetailsXaml();

        var title = Regex.Match(xaml, @"<TextBlock Text=""\{Binding Title\}""[\s\S]*?</TextBlock>");
        Assert.True(title.Success, "Spaltentitel nicht gefunden.");
        Assert.Contains("PreviewMouseLeftButtonDown=\"ColumnHandle_PreviewMouseLeftButtonDown\"", title.Value);
        Assert.Contains("PreviewMouseMove=\"ColumnHandle_PreviewMouseMove\"", title.Value);

        Assert.Contains("PreviewDragOver=\"ColumnCard_PreviewDragOver\"", xaml);
        Assert.Contains("PreviewDrop=\"ColumnCard_PreviewDrop\"", xaml);
    }

    [Fact]
    public void Die_Spalten_verteilen_sich_gleichmaessig_auf_die_Breite()
    {
        var xaml = ReadDetailsXaml();

        var panel = Regex.Match(xaml, @"<ItemsPanelTemplate x:Key=""CompactDetailGroupPanel"">[\s\S]*?</ItemsPanelTemplate>");
        Assert.True(panel.Success, "Spaltenraster nicht gefunden.");

        // Ein UniformGrid mit einer Zeile: die Spaltenzahl folgt der Zahl der Gruppen.
        Assert.Contains("<UniformGrid Rows=\"1\"/>", panel.Value);

        // Feste Pixelbreiten liessen rechts einen leeren Streifen stehen, sobald eine
        // Spalte weniger da ist.
        Assert.DoesNotContain("ColumnDefinition", panel.Value);
    }

    [Fact]
    public void Angezeigt_wird_die_gefilterte_Spaltenliste()
    {
        var xaml = ReadDetailsXaml();
        var code = ReadFile("src", "AuswertungPro.Next.UI", "Views", "Controls", "RecordDetailsView.xaml.cs");

        // Nicht die rohe Gruppenliste: sonst bliebe eine leergeraeumte Spalte als
        // leerer Kasten stehen und naehme den anderen die Breite weg.
        Assert.Contains("ItemsSource=\"{Binding VisibleGroups, ElementName=Root}\"", xaml);
        Assert.DoesNotContain("ItemsSource=\"{Binding Groups, ElementName=Root}\"", xaml);
        Assert.Contains("RecordDetailColumnVisibility.Filter(groups, IsCustomizing, IsCompactLayout)", code);
    }

    [Fact]
    public void Eine_umschlagende_Karte_teilt_die_Spalten_sofort_neu_auf()
    {
        var code = ReadFile("src", "AuswertungPro.Next.UI", "Views", "Controls", "RecordDetailsView.xaml.cs");

        // "Sanieren = Nein" macht Karten zur Laufzeit unsichtbar. Ohne das Abo bliebe
        // die dann leere Spalte stehen, bis der Datensatz gewechselt wird.
        var handler = Regex.Match(
            code,
            @"private void Item_VisibilityChanged\([\s\S]*?\n    \}");
        Assert.True(handler.Success, "Abo auf die Sichtbarkeit der Karten nicht gefunden.");
        Assert.Contains("IsVisible", handler.Value);
        Assert.Contains("IsHiddenByUser", handler.Value);
        Assert.Contains("RefreshVisibleGroups();", handler.Value);
    }

    // --- Ausgeblendete Felder ----------------------------------------------

    [Fact]
    public void Ausgeblendete_Felder_lassen_sich_zurueckholen()
    {
        var xaml = ReadDetailsXaml();

        Assert.Contains("Text=\"Ausgeblendete Felder\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding HiddenFields, ElementName=Root}\"", xaml);
        Assert.Contains("Click=\"ShowField_Click\"", xaml);
    }

    // --- Beide Ansichten ----------------------------------------------------

    [Theory]
    // Haltungen und Schaechte haben getrennte Layouts und getrennte Speicherplaetze.
    [InlineData("Haltungsansicht", "HaltungsansichtView.xaml.cs", "DataPageLayout")]
    [InlineData("Schachtansicht", "SchachtansichtView.xaml.cs", "SchaechtePageLayout")]
    public void Ansicht_bietet_den_Anpassen_Modus_und_speichert_ihn(string folder, string file, string layoutProperty)
    {
        var code = ReadFile("src", "AuswertungPro.Next.UI", "Views", "Pages", folder, file);

        Assert.Contains("Detail.CanCustomize = true;", code);
        Assert.Contains("Detail.LayoutChanged += Detail_LayoutChanged;", code);
        Assert.Contains("Detail.LayoutResetRequested += Detail_LayoutResetRequested;", code);

        // Gespeicherte Gestaltung wird beim Aufbau angewandt ...
        Assert.Contains("RecordDetailLayoutApplier.Apply(", code);
        Assert.Contains($"RecordDetailLayoutSettingsMapper.ToLayout(_settings?.{layoutProperty}?.DetailLayout)", code);

        // ... und eine Aenderung landet in genau derselben Einstellung.
        var handler = Regex.Match(
            code,
            @"private void Detail_LayoutChanged\(object\? sender, RecordDetailLayoutChangedEventArgs e\)[\s\S]*?\n    \}");
        Assert.True(handler.Success, $"Speicher-Handler in {file} nicht gefunden.");
        Assert.Contains($"_settings?.{layoutProperty}", handler.Value);
        Assert.Contains("RecordDetailLayoutSettingsMapper.ToSettings(e.Layout)", handler.Value);
        Assert.Contains(".Save();", handler.Value);
    }

    [Theory]
    [InlineData("Haltungsansicht", "HaltungsansichtView.xaml.cs", "DataPageLayout")]
    [InlineData("Schachtansicht", "SchachtansichtView.xaml.cs", "SchaechtePageLayout")]
    public void Standard_wiederherstellen_leert_die_gespeicherte_Gestaltung(string folder, string file, string layoutProperty)
    {
        var code = ReadFile("src", "AuswertungPro.Next.UI", "Views", "Pages", folder, file);

        var handler = Regex.Match(
            code,
            @"private void Detail_LayoutResetRequested\(object\? sender, EventArgs e\)[\s\S]*?\n    \}");
        Assert.True(handler.Success, $"Zuruecksetzen-Handler in {file} nicht gefunden.");
        Assert.Contains($"_settings?.{layoutProperty}", handler.Value);
        Assert.Contains("new DetailLayoutSettings()", handler.Value);
        Assert.Contains(".Save();", handler.Value);
    }

    // --- Grenze zur Fachlogik ----------------------------------------------

    [Theory]
    [InlineData("RecordDetailLayoutApplier.cs")]
    [InlineData("RecordDetailDragOperations.cs")]
    [InlineData("RecordDetailOrderRanking.cs")]
    public void Die_fachliche_Feldreihenfolge_bleibt_unberuehrt(string fileName)
    {
        var source = ReadFile("src", "AuswertungPro.Next.UI", "DataPage", fileName);

        // Kommentare duerfen den Katalog erwaehnen - der ausgefuehrte Code nicht.
        var code = string.Join(
            "\n",
            source.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        // An FieldCatalog.ColumnOrder haengen CSV-/Excel-Export und der Import-Merge.
        // Die persoenliche Gestaltung darf sie nie lesen und schon gar nicht umschreiben.
        Assert.DoesNotContain("FieldCatalog", code);
        Assert.DoesNotContain("AuswertungPro.Next.Domain", code);
    }
}
