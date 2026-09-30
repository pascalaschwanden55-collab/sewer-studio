using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13, Fix-Runde 4 (Befund E): ersetzt die Gegenrichtungspruefung
/// aus Fix-Runde 3, die nur zwei Muster kannte (Attributpaar am selben Element, Setterpaar im selben
/// Trigger) und deshalb blind war fuer
/// - Vorlagen-Attribut + Style-Setter (ToolbarButtonAccent: Flaeche im Template, Schrift im Style),
/// - ein Kind-TextBlock mit eigener Schrift in einer Akzentflaeche (HaltungsansichtView),
/// - Knopfinhalt mit eigener Schrift (PhotoMeasurementWindow BtnUndo),
/// - TargetName-Trigger, die nur die Flaeche umstellen (PresetBtn; PrimaryButton erbte die
///   Hover-Toenung der Basisvorlage - weisse Schrift auf 4 % Akzent).
///
/// Jetzt baut <see cref="XamlFarbpaarModell"/> je Style/Vorlage/Ansicht und je Zustand das wirksame
/// Paar aus Flaeche und Schrift jedes Text-/Symbol-Elements (BasedOn, TemplateBinding, TargetName,
/// Vererbung, impliziter TextBlock-Stil, Knopfinhalt am ContentPresenter), fuer das helle UND das
/// dunkle Theme. Geprueft wird gegen die Zuordnung in ThemeHighContrast.xaml:
/// R1 Highlight-Flaeche nur mit HighlightText-Schrift; eine andere Systemflaeche nie mit literaler
///    Schrift oder HighlightText-Schrift.
/// R2 "Text auf Akzent" (OnAccentBrush u. Ae.) nur auf einer Flaeche, die unter Hochkontrast auf
///    Highlight faellt - in den normalen Themes ist diese Schrift Weiss.
/// Jeder Fundweg hat unten einen eigenen Pruefling (Sabotageprobe als dauerhafter Test).
/// </summary>
public sealed class ThemeHighContrastFarbpaarTests
{
    /// <summary>Schriftfarben, die ausschliesslich AUF einer gefuellten Akzentflaeche stehen duerfen.
    /// AccentTextBrush gehoert NICHT dazu: das ist Akzent-FARBIGE Schrift auf normalen Flaechen.</summary>
    private static readonly HashSet<string> TextAufAkzent = new(System.StringComparer.Ordinal)
    {
        "OnAccentBrush", "SelectionTextBrush", "NavSelectedTextBrush",
    };

    /// <summary>
    /// Bewusst belassene Befunde - je Eintrag Datei, ein eindeutiger Teil der Meldung und der Grund.
    /// Ein Eintrag, der nichts mehr trifft, macht <see cref="Bekannte_Ausnahmen_treffen_noch_etwas"/>
    /// rot (die Liste bleibt ehrlich).
    /// </summary>
    private static readonly (string Datei, string Teil, string Grund)[] BekannteAusnahmen =
    {
        ("WebGisHolenWindow.xaml", "FluentIcon",
            "Geschuetzte Datei (Global Constraint 2, fremde Arbeit): das Symbol im Uebernehmen-Knopf ist "
            + "hart Weiss auf AccentBrush. Sobald die Datei freigegeben ist: Foreground=OnAccentBrush."),
        ("TrainingStudioWindow.xaml", "DeepSkyBlue",
            "Legende der blauen Vorschau-Boxen: dieselbe Farbe zeichnet TrainingStudioWindow.xaml.cs "
            + "(Brushes.DeepSkyBlue) als Rahmen ins Foto - Text und Rahmen muessen gleich bleiben."),
    };

    [Fact]
    public void Kein_Text_und_kein_Symbol_verliert_unter_Hochkontrast_seinen_Kontrast()
    {
        var verstoesse = AlleVerstoesse()
            .Where(v => !BekannteAusnahmen.Any(a => IstAusnahme(v, a.Datei, a.Teil)))
            .ToList();

        Assert.True(verstoesse.Count == 0,
            $"{verstoesse.Count} Farbpaare verlieren unter Hochkontrast ihren Partner:\n"
            + string.Join("\n", verstoesse.Take(200)));
    }

    [Fact]
    public void Bekannte_Ausnahmen_treffen_noch_etwas()
    {
        var alle = AlleVerstoesse();
        foreach (var (datei, teil, _) in BekannteAusnahmen)
            Assert.True(alle.Any(v => IstAusnahme(v, datei, teil)), $"Ausnahme verwaist (bitte entfernen): {datei} / {teil}");
    }

    private static bool IstAusnahme(string verstoss, string datei, string teil)
        => verstoss.Contains($" {datei}:", System.StringComparison.Ordinal)
           && verstoss.Contains(teil, System.StringComparison.Ordinal);

    private static List<string> AlleVerstoesse()
    {
        var hc = LiesHochkontrastZuordnung();
        var verstoesse = new SortedSet<string>(System.StringComparer.Ordinal);

        foreach (var (theme, modell) in Modelle())
        {
            foreach (var datei in TestXaml.Alle())
            {
                var name = Path.GetFileName(datei);
                if (name == "ThemeHighContrast.xaml")
                    continue;
                if (name == "Theme.xaml" && theme != "Dunkel")
                    continue;
                if (name == "ThemeLight.xaml" && theme != "Hell")
                    continue;

                var wurzel = XDocument.Load(datei).Root!;
                var paare = modell.SammlePaare(wurzel, name);
                foreach (var v in XamlFarbpaarModell.Pruefe(paare, hc, TextAufAkzent))
                    verstoesse.Add($"[{theme}] {v}");
            }
        }

        return verstoesse.ToList();
    }

    // ── Dauerhafte Sabotageproben: jeder Fundweg einzeln ────────────────────────────────────

    [Fact]
    public void Fundweg_Attributpaar_am_selben_Element()
    {
        var v = Pruefe("""
            <Border Background="{DynamicResource AccentBrush}">
              <TextBlock Text="x" Foreground="White"/>
            </Border>
            """);
        Assert.Contains(v, x => x.Contains("TextBlock") && x.Contains("White"));

        // Gegenprobe: richtiges Paar
        Assert.Empty(Pruefe("""
            <Border Background="{DynamicResource AccentBrush}">
              <TextBlock Text="x" Foreground="{DynamicResource OnAccentBrush}"/>
            </Border>
            """));
    }

    [Fact]
    public void Fundweg_Style_Setterpaar_im_Trigger()
    {
        var v = Pruefe("""
            <Grid>
              <Grid.Resources>
                <Style x:Key="Probe" TargetType="ListBoxItem">
                  <Setter Property="Foreground" Value="{DynamicResource TextBrush}"/>
                  <Style.Triggers>
                    <Trigger Property="IsSelected" Value="True">
                      <Setter Property="Background" Value="{DynamicResource AccentBrush}"/>
                      <Setter Property="Foreground" Value="White"/>
                    </Trigger>
                  </Style.Triggers>
                </Style>
              </Grid.Resources>
            </Grid>
            """);
        Assert.Contains(v, x => x.Contains("Style Probe") && x.Contains("IsSelected=True"));
    }

    [Fact]
    public void Fundweg_Vorlagenattribut_mit_Style_Setter()
    {
        // ToolbarButtonAccent-Muster: die Flaeche steht als Attribut in der Vorlage, die Schrift als
        // Setter im Style.
        var v = Pruefe("""
            <Grid>
              <Grid.Resources>
                <Style x:Key="Probe" TargetType="Button">
                  <Setter Property="Foreground" Value="White"/>
                  <Setter Property="Template">
                    <Setter.Value>
                      <ControlTemplate TargetType="Button">
                        <Border x:Name="bd" Background="{DynamicResource AccentBrush}">
                          <ContentPresenter>
                            <ContentPresenter.Resources>
                              <Style TargetType="TextBlock">
                                <Setter Property="Foreground" Value="{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}"/>
                              </Style>
                            </ContentPresenter.Resources>
                          </ContentPresenter>
                        </Border>
                      </ControlTemplate>
                    </Setter.Value>
                  </Setter>
                </Style>
              </Grid.Resources>
            </Grid>
            """);
        Assert.Contains(v, x => x.Contains("Style Probe") && x.Contains("ContentPresenter") && x.Contains("White"));
    }

    [Fact]
    public void Fundweg_Knopfinhalt_mit_eigener_Schrift()
    {
        // BtnUndo-Muster: Flaeche lokal am Knopf, Schrift im Style, ein Symbol im Inhalt mit Weiss.
        var v = Pruefe("""
            <Grid>
              <Grid.Resources>
                <Style x:Key="Probe" TargetType="Button">
                  <Setter Property="Foreground" Value="{DynamicResource OnAccentBrush}"/>
                  <Setter Property="Template">
                    <Setter.Value>
                      <ControlTemplate TargetType="Button">
                        <Border x:Name="Bd" Background="{TemplateBinding Background}">
                          <ContentPresenter/>
                        </Border>
                      </ControlTemplate>
                    </Setter.Value>
                  </Setter>
                </Style>
              </Grid.Resources>
              <Button Style="{StaticResource Probe}" Background="{DynamicResource AccentBrush}">
                <StackPanel>
                  <ui:FluentIcon Glyph="x" Foreground="White"/>
                  <TextBlock Text="ok"/>
                </StackPanel>
              </Button>
            </Grid>
            """);
        Assert.Contains(v, x => x.Contains("FluentIcon") && x.Contains("White"));
        // Der TextBlock ohne eigene Schrift bekommt den impliziten TextBlock-Stil (TextBrush) -
        // auf einer Akzentflaeche ebenfalls falsch (kein B7-Durchreich-Stil in dieser Vorlage).
        Assert.Contains(v, x => x.Contains("> TextBlock") && x.Contains("TextBrush"));
    }

    [Fact]
    public void Fundweg_TargetName_Trigger_stellt_nur_die_Flaeche_um()
    {
        // PresetBtn/PrimaryButton-Muster: der Trigger aendert nur die benannte Flaeche, die Schrift
        // bleibt die des Grundzustands (hier: BasedOn-Kette, Schrift im abgeleiteten Style).
        var v = Pruefe("""
            <Grid>
              <Grid.Resources>
                <Style x:Key="Basis" TargetType="Button">
                  <Setter Property="Foreground" Value="{DynamicResource TextBrush}"/>
                  <Setter Property="Template">
                    <Setter.Value>
                      <ControlTemplate TargetType="Button">
                        <Border x:Name="MainBorder" Background="{TemplateBinding Background}">
                          <ContentPresenter>
                            <ContentPresenter.Resources>
                              <Style TargetType="TextBlock">
                                <Setter Property="Foreground" Value="{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}"/>
                              </Style>
                            </ContentPresenter.Resources>
                          </ContentPresenter>
                        </Border>
                        <ControlTemplate.Triggers>
                          <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="MainBorder" Property="Background" Value="{DynamicResource AccentHoverTintBrush}"/>
                          </Trigger>
                        </ControlTemplate.Triggers>
                      </ControlTemplate>
                    </Setter.Value>
                  </Setter>
                </Style>
                <Style x:Key="Probe" TargetType="Button" BasedOn="{StaticResource Basis}">
                  <Setter Property="Background" Value="{DynamicResource AccentBrush}"/>
                  <Setter Property="Foreground" Value="{DynamicResource OnAccentBrush}"/>
                </Style>
              </Grid.Resources>
            </Grid>
            """);
        Assert.Contains(v, x => x.StartsWith("R2") && x.Contains("Style Probe") && x.Contains("IsMouseOver=True")
                                && x.Contains("AccentHoverTintBrush"));
        // Grundzustand derselben Probe ist korrekt und darf nicht gemeldet werden.
        Assert.DoesNotContain(v, x => x.Contains("Style Probe") && x.Contains("[Grundzustand]"));
    }

    [Fact]
    public void Fundweg_DataTemplate_TargetName_Trigger()
    {
        var v = Pruefe("""
            <ItemsControl>
              <ItemsControl.ItemTemplate>
                <DataTemplate>
                  <Border x:Name="Zeile" Background="{DynamicResource CardBrush}">
                    <TextBlock Text="{Binding}" Foreground="White"/>
                  </Border>
                  <DataTemplate.Triggers>
                    <DataTrigger Binding="{Binding Aktiv}" Value="True">
                      <Setter TargetName="Zeile" Property="Background" Value="{DynamicResource AccentBrush}"/>
                    </DataTrigger>
                  </DataTemplate.Triggers>
                </DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>
            """);
        Assert.Contains(v, x => x.Contains("Binding {Binding Aktiv}=True") && x.Contains("Flaeche Highlight"));
        Assert.Contains(v, x => x.Contains("[Grundzustand]") && x.Contains("Flaeche Control, Schrift literal"));
    }

    [Fact]
    public void Eingeklapptes_Symbol_zaehlt_nur_wo_es_sichtbar_ist()
    {
        // CheckBox-Haken: im Grundzustand eingeklappt, erst bei IsChecked auf der Akzentflaeche.
        Assert.Empty(Pruefe("""
            <Grid>
              <Grid.Resources>
                <Style x:Key="Probe" TargetType="CheckBox">
                  <Setter Property="Template">
                    <Setter.Value>
                      <ControlTemplate TargetType="CheckBox">
                        <Border x:Name="CheckBorder" Background="{DynamicResource CardBrush}">
                          <Path x:Name="CheckMark" Stroke="{DynamicResource OnAccentBrush}" Visibility="Collapsed"/>
                        </Border>
                        <ControlTemplate.Triggers>
                          <Trigger Property="IsChecked" Value="True">
                            <Setter TargetName="CheckBorder" Property="Background" Value="{DynamicResource AccentBrush}"/>
                            <Setter TargetName="CheckMark" Property="Visibility" Value="Visible"/>
                          </Trigger>
                        </ControlTemplate.Triggers>
                      </ControlTemplate>
                    </Setter.Value>
                  </Setter>
                </Style>
              </Grid.Resources>
            </Grid>
            """));
    }

    // ── Hilfen ──────────────────────────────────────────────────────────────────────────────

    private static List<string> Pruefe(string xamlKoerper)
    {
        var xaml = $"""
            <UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:ui="clr-namespace:AuswertungPro.Next.UI">
            {xamlKoerper}
            </UserControl>
            """;
        var probeWurzel = XDocument.Parse(xaml).Root!;
        var modell = new XamlFarbpaarModell(GlobaleWoerterbuecher("ThemeLight.xaml"));
        var paare = modell.SammlePaare(probeWurzel, "Probe.xaml");
        return XamlFarbpaarModell.Pruefe(paare, LiesHochkontrastZuordnung(), TextAufAkzent);
    }

    private static IEnumerable<(string Theme, XamlFarbpaarModell Modell)> Modelle()
    {
        yield return ("Hell", new XamlFarbpaarModell(GlobaleWoerterbuecher("ThemeLight.xaml")));
        yield return ("Dunkel", new XamlFarbpaarModell(GlobaleWoerterbuecher("Theme.xaml")));
    }

    /// <summary>Suchreihenfolge wie in WPF: eigene Eintraege von App.xaml, danach die eingemischten
    /// Woerterbuecher von hinten nach vorn (NovaPageHeader, Controls, Theme).</summary>
    private static IEnumerable<XElement> GlobaleWoerterbuecher(string themeDatei)
    {
        var app = XDocument.Load(RepoFile("src", "AuswertungPro.Next.UI", "App.xaml")).Root!;
        var appWoerterbuch = app.Descendants().First(e => e.Name.LocalName == "ResourceDictionary");
        yield return new XElement(appWoerterbuch.Name,
            appWoerterbuch.Elements().Where(e => !e.Name.LocalName.Contains('.')));
        yield return XDocument.Load(RepoFile("src", "AuswertungPro.Next.UI", "Controls", "NovaPageHeader.xaml")).Root!;
        yield return XDocument.Load(RepoFile("src", "AuswertungPro.Next.UI", "Theme", "Controls.xaml")).Root!;
        yield return XDocument.Load(RepoFile("src", "AuswertungPro.Next.UI", "Theme", themeDatei)).Root!;
    }

    /// <summary>Schluessel -&gt; SystemColors-Name ("Highlight", "HighlightText", "Window", ...)
    /// direkt aus ThemeHighContrast.xaml.</summary>
    private static Dictionary<string, string> LiesHochkontrastZuordnung()
    {
        var doc = XDocument.Load(RepoFile("src", "AuswertungPro.Next.UI", "Theme", "ThemeHighContrast.xaml"));
        var x = doc.Root!.GetNamespaceOfPrefix("x")!;
        var zuordnung = new Dictionary<string, string>(System.StringComparer.Ordinal);
        foreach (var e in doc.Root.Elements())
        {
            var key = e.Attribute(x + "Key")?.Value;
            var farbe = e.Attribute("Color")?.Value;
            if (key is null || farbe is null)
                continue;
            var m = System.Text.RegularExpressions.Regex.Match(farbe, @"SystemColors\.([A-Za-z]+)ColorKey");
            if (m.Success)
                zuordnung[key] = m.Groups[1].Value;
        }

        return zuordnung;
    }

}
