using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Q4b (Wartbarkeitsaudit 30.09.2026): Verhaltenstests fuer <see cref="XamlFarbpaarModell"/> an
/// kleinen XAML-Zeichenketten. Das Modell ist ein eigener XAML-Zustandsparser (rund 900 Zeilen);
/// bisher pruefte es nur <see cref="ThemeHighContrastFarbpaarTests"/> gegen die echten Dateien.
/// Ein Fehler im Modell haette dort entweder alles rot oder — schlimmer — still gruen gemacht.
/// Diese Tests halten die Rangfolge-Regeln aus der Klassendoku und aus CLAUDE.md
/// (Aufgabe 13, Fix-Runde 4) einzeln fest. Die Woerterbuecher sind leer: jede Probe bringt ihre
/// Stile selbst mit, damit keine Theme-Aenderung die Erwartung verschiebt.
/// </summary>
public sealed class XamlFarbpaarModellTests
{
    private const string Kopf =
        "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
        "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    private static List<Farbpaar> Sammle(string koerper)
    {
        var wurzel = XDocument.Parse($"<UserControl {Kopf}>{koerper}</UserControl>").Root!;
        return new XamlFarbpaarModell(Array.Empty<XElement>()).SammlePaare(wurzel, "Probe.xaml");
    }

    private static Farbpaar[] Zustand(List<Farbpaar> paare, string ortTeil, string zustandTeil)
        => paare.Where(p => p.Ort.Contains(ortTeil, StringComparison.Ordinal)
                            && p.Zustand.Contains(zustandTeil, StringComparison.Ordinal)).ToArray();

    [Fact]
    public void Grundzustand_liefert_Schrift_auf_der_naechsten_Flaeche()
    {
        var paare = Sammle("""
            <Border Background="{DynamicResource CardBrush}">
              <TextBlock Text="a" Foreground="{DynamicResource TextBrush}"/>
            </Border>
            """);

        var p = Assert.Single(paare);
        Assert.Equal("Grundzustand", p.Zustand);
        Assert.Equal("{DynamicResource CardBrush}", p.Hintergrund);
        Assert.Equal("{DynamicResource TextBrush}", p.Vordergrund);
    }

    [Fact]
    public void Style_Trigger_schlaegt_den_Style_Setter_und_der_Grundzustand_bleibt()
    {
        var paare = Sammle("""
            <Style x:Key="Probe" TargetType="Button">
              <Setter Property="Background" Value="{DynamicResource CardBrush}"/>
              <Setter Property="Foreground" Value="{DynamicResource TextBrush}"/>
              <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                  <Setter Property="Background" Value="{DynamicResource AccentBrush}"/>
                </Trigger>
              </Style.Triggers>
            </Style>
            """);

        var grund = Assert.Single(Zustand(paare, "Style Probe", "Grundzustand"));
        Assert.Equal("{DynamicResource CardBrush}", grund.Hintergrund);

        var hover = Assert.Single(Zustand(paare, "Style Probe", "IsMouseOver"));
        Assert.Equal("{DynamicResource AccentBrush}", hover.Hintergrund);
        // Die Schrift stammt weiter aus dem Setter: der Trigger hat sie nicht angefasst.
        Assert.Equal("{DynamicResource TextBrush}", hover.Vordergrund);
    }

    [Fact]
    public void TargetName_Trigger_in_der_Vorlage_faerbt_nur_das_benannte_Element()
    {
        var paare = Sammle("""
            <Style x:Key="Probe" TargetType="Button">
              <Setter Property="Foreground" Value="{DynamicResource TextBrush}"/>
              <Setter Property="Template">
                <Setter.Value>
                  <ControlTemplate TargetType="Button">
                    <Border x:Name="Rahmen" Background="{DynamicResource CardBrush}">
                      <ContentPresenter/>
                    </Border>
                    <ControlTemplate.Triggers>
                      <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="Rahmen" Property="Background" Value="{DynamicResource AccentBrush}"/>
                      </Trigger>
                    </ControlTemplate.Triggers>
                  </ControlTemplate>
                </Setter.Value>
              </Setter>
            </Style>
            """);

        var grund = Zustand(paare, "Style Probe", "Grundzustand");
        Assert.Contains(grund, p => p.Hintergrund == "{DynamicResource CardBrush}" && p.Vordergrund == "{DynamicResource TextBrush}");

        var hover = Zustand(paare, "Style Probe", "IsMouseOver");
        Assert.NotEmpty(hover);
        Assert.All(hover, p => Assert.Equal("{DynamicResource AccentBrush}", p.Hintergrund));
    }

    [Fact]
    public void BasedOn_erbt_die_Basis_und_der_abgeleitete_Setter_gewinnt()
    {
        var paare = Sammle("""
            <Grid><Grid.Resources>
              <Style x:Key="Basis" TargetType="Button">
                <Setter Property="Background" Value="{DynamicResource CardBrush}"/>
                <Setter Property="Foreground" Value="{DynamicResource TextBrush}"/>
              </Style>
              <Style x:Key="Abgeleitet" TargetType="Button" BasedOn="{StaticResource Basis}">
                <Setter Property="Foreground" Value="{DynamicResource DangerTextBrush}"/>
              </Style>
            </Grid.Resources></Grid>
            """);

        var p = Assert.Single(Zustand(paare, "Style Abgeleitet", "Grundzustand"));
        Assert.Equal("{DynamicResource CardBrush}", p.Hintergrund);          // von der Basis geerbt
        Assert.Equal("{DynamicResource DangerTextBrush}", p.Vordergrund);    // abgeleitet gewinnt
    }

    [Fact]
    public void IsPressed_schliesst_IsMouseOver_ein()
    {
        var paare = Sammle("""
            <Style x:Key="Probe" TargetType="Button">
              <Setter Property="Background" Value="{DynamicResource CardBrush}"/>
              <Setter Property="Foreground" Value="{DynamicResource TextBrush}"/>
              <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                  <Setter Property="Background" Value="{DynamicResource HoverBrush}"/>
                </Trigger>
                <Trigger Property="IsPressed" Value="True">
                  <Setter Property="Foreground" Value="{DynamicResource OnAccentBrush}"/>
                </Trigger>
              </Style.Triggers>
            </Style>
            """);

        // Beim Druecken greifen BEIDE Trigger: Hover-Flaeche UND Druck-Schrift.
        var gedrueckt = Zustand(paare, "Style Probe", "IsPressed");
        var p = Assert.Single(gedrueckt);
        Assert.Equal("{DynamicResource HoverBrush}", p.Hintergrund);
        Assert.Equal("{DynamicResource OnAccentBrush}", p.Vordergrund);
    }

    [Fact]
    public void Eigener_TextBlock_im_Knopfinhalt_bekommt_den_impliziten_TextBlock_Stil_der_Ansicht()
    {
        // Zur Laufzeit belegt (CLAUDE.md, Fix-Runde 4): Der eigene TextBlock sucht seinen impliziten
        // Stil im LOGISCHEN Baum, nicht in der Knopfvorlage — er bekommt TextBrush, nicht die
        // weisse Knopfschrift.
        var paare = Sammle("""
            <Grid>
              <Grid.Resources>
                <Style TargetType="TextBlock">
                  <Setter Property="Foreground" Value="{DynamicResource TextBrush}"/>
                </Style>
                <Style x:Key="Gefuellt" TargetType="Button">
                  <Setter Property="Background" Value="{DynamicResource AccentBrush}"/>
                  <Setter Property="Foreground" Value="White"/>
                  <Setter Property="Template">
                    <Setter.Value>
                      <ControlTemplate TargetType="Button">
                        <Border Background="{TemplateBinding Background}"><ContentPresenter/></Border>
                      </ControlTemplate>
                    </Setter.Value>
                  </Setter>
                </Style>
              </Grid.Resources>
              <Button Style="{StaticResource Gefuellt}">
                <StackPanel><TextBlock Text="a"/></StackPanel>
              </Button>
            </Grid>
            """);

        var inhalt = paare.Where(p => p.Ort.Contains("Inhalt", StringComparison.Ordinal)
                                      && p.Ort.Contains("TextBlock", StringComparison.Ordinal)).ToArray();
        var p = Assert.Single(inhalt);
        Assert.Equal("{DynamicResource AccentBrush}", p.Hintergrund);
        Assert.Equal("{DynamicResource TextBrush}", p.Vordergrund);
    }

    [Fact]
    public void TemplateBinding_liest_die_wirksame_Eigenschaft_des_Bedienelements_je_Zustand()
    {
        var paare = Sammle("""
            <Style x:Key="Probe" TargetType="Button">
              <Setter Property="Background" Value="{DynamicResource CardBrush}"/>
              <Setter Property="Foreground" Value="{DynamicResource TextBrush}"/>
              <Setter Property="Template">
                <Setter.Value>
                  <ControlTemplate TargetType="Button">
                    <Border Background="{TemplateBinding Background}"><ContentPresenter/></Border>
                  </ControlTemplate>
                </Setter.Value>
              </Setter>
              <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                  <Setter Property="Background" Value="{DynamicResource AccentBrush}"/>
                </Trigger>
              </Style.Triggers>
            </Style>
            """);

        Assert.Contains(Zustand(paare, "Style Probe", "Grundzustand"), p => p.Hintergrund == "{DynamicResource CardBrush}");
        var hover = Zustand(paare, "Style Probe", "IsMouseOver");
        Assert.NotEmpty(hover);
        Assert.All(hover, p => Assert.Equal("{DynamicResource AccentBrush}", p.Hintergrund));
    }

    [Fact]
    public void Ein_in_einem_Zustand_eingeklapptes_Element_zaehlt_dort_nicht()
    {
        var paare = Sammle("""
            <Style x:Key="Probe" TargetType="Button">
              <Setter Property="Foreground" Value="{DynamicResource TextBrush}"/>
              <Setter Property="Template">
                <Setter.Value>
                  <ControlTemplate TargetType="Button">
                    <Border Background="{DynamicResource CardBrush}">
                      <TextBlock x:Name="Marke" Text="a" Visibility="Collapsed"/>
                    </Border>
                    <ControlTemplate.Triggers>
                      <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="Marke" Property="Visibility" Value="Visible"/>
                      </Trigger>
                    </ControlTemplate.Triggers>
                  </ControlTemplate>
                </Setter.Value>
              </Setter>
            </Style>
            """);

        Assert.Empty(Zustand(paare, "TextBlock", "Grundzustand"));
        Assert.NotEmpty(Zustand(paare, "TextBlock", "IsMouseOver"));
    }
}
