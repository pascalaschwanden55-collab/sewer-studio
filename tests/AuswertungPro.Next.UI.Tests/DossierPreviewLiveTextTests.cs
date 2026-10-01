using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using AuswertungPro.Next.UI.Views.Rendering;

using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Der getippte Text erscheint sofort im Blatt.
///
/// Gemessen: Eine echte Word/PDF-Umwandlung dauert auf diesem Rechner 2,35 s
/// (mit geteiltem LibreOffice-Profil rund 1,0 s). Echtzeit ist darüber nicht
/// zu haben — eine kürzere Schreibpause würde nur mehr Umwandlungen stapeln.
///
/// Deshalb wird der neue Text unmittelbar über die alte Stelle gelegt und das
/// echte Bild zieht still nach. Die Sofortanzeige ist bewusst eine Näherung:
/// Sie zeigt den Wortlaut, nicht den endgültigen Umbruch. Sobald die echte
/// Ausgabe da ist, ersetzt sie das ganze Blatt und damit auch diese Näherung.
/// </summary>
public sealed class DossierPreviewLiveTextTests
{
    private static Border Rahmen(double links, double oben, double breite, double hoehe)
    {
        var rahmen = new Border { Width = breite, Height = hoehe };
        Canvas.SetLeft(rahmen, links);
        Canvas.SetTop(rahmen, oben);
        return rahmen;
    }

    [Fact]
    public void Der_neue_Text_liegt_ueber_der_alten_Stelle()
    {
        RunOnSta(() =>
        {
            var blatt = new Canvas { Width = 800, Height = 1100 };
            var stelle = Rahmen(100, 200, 60, 14);
            blatt.Children.Add(stelle);

            DossierPreviewLiveText.Zeige(blatt, [stelle], "Neuer Wortlaut");

            var live = blatt.Children.OfType<Border>()
                .Single(kind => DossierPreviewLiveText.IstSofortanzeige(kind));

            Assert.Equal(100, Canvas.GetLeft(live));
            Assert.Equal(200, Canvas.GetTop(live));
            Assert.Equal(60, live.Width);

            var text = Assert.IsType<TextBlock>(live.Child);
            Assert.Equal("Neuer Wortlaut", text.Text);
        });
    }

    [Fact]
    public void Mehrere_Woerter_einer_Stelle_werden_zusammengefasst()
    {
        // Ein Feld kann ueber mehrere Woerter gehen; die Sofortanzeige deckt
        // sie gemeinsam ab, sonst blieben Reste des alten Textes stehen.
        RunOnSta(() =>
        {
            var blatt = new Canvas { Width = 800, Height = 1100 };
            var eins = Rahmen(100, 200, 40, 14);
            var zwei = Rahmen(150, 200, 70, 14);
            blatt.Children.Add(eins);
            blatt.Children.Add(zwei);

            DossierPreviewLiveText.Zeige(blatt, [eins, zwei], "Karl Theodor Dittli");

            var live = blatt.Children.OfType<Border>()
                .Single(kind => DossierPreviewLiveText.IstSofortanzeige(kind));

            Assert.Equal(100, Canvas.GetLeft(live));
            Assert.Equal(120, live.Width);
        });
    }

    [Fact]
    public void Eine_zweite_Eingabe_ersetzt_die_erste()
    {
        // Sonst stapelten sich bei jedem Tastendruck die Schichten.
        RunOnSta(() =>
        {
            var blatt = new Canvas { Width = 800, Height = 1100 };
            var stelle = Rahmen(10, 20, 50, 14);
            blatt.Children.Add(stelle);

            DossierPreviewLiveText.Zeige(blatt, [stelle], "Erst");
            DossierPreviewLiveText.Zeige(blatt, [stelle], "Dann");

            var live = blatt.Children.OfType<Border>()
                .Single(kind => DossierPreviewLiveText.IstSofortanzeige(kind));

            Assert.Equal("Dann", ((TextBlock)live.Child).Text);
        });
    }

    [Fact]
    public void Ohne_Stelle_im_Blatt_passiert_nichts()
    {
        // Ein leeres Feld hat im Blatt keine Woerter. Dann gibt es nichts zu
        // ueberdecken — und es wird auch nichts erfunden.
        RunOnSta(() =>
        {
            var blatt = new Canvas { Width = 800, Height = 1100 };

            DossierPreviewLiveText.Zeige(blatt, [], "Text ohne Stelle");

            Assert.Empty(blatt.Children.OfType<Border>()
                .Where(DossierPreviewLiveText.IstSofortanzeige));
        });
    }

    [Fact]
    public void Ein_geleertes_Feld_deckt_den_alten_Text_zu()
    {
        // Wer den Text loescht, muss ihn sofort verschwinden sehen — sonst
        // stuende er noch zwei Sekunden im Blatt.
        RunOnSta(() =>
        {
            var blatt = new Canvas { Width = 800, Height = 1100 };
            var stelle = Rahmen(10, 20, 50, 14);
            blatt.Children.Add(stelle);

            DossierPreviewLiveText.Zeige(blatt, [stelle], "");

            var live = blatt.Children.OfType<Border>()
                .Single(kind => DossierPreviewLiveText.IstSofortanzeige(kind));

            Assert.Equal(string.Empty, ((TextBlock)live.Child).Text);
            Assert.Equal(Brushes.White.Color, ((SolidColorBrush)live.Background).Color);
        });
    }

    [Fact]
    public void Die_Schriftgroesse_folgt_der_Zeilenhoehe()
    {
        // Eine feste Groesse saehe auf dem Deckblatt winzig und in der Tabelle
        // riesig aus.
        RunOnSta(() =>
        {
            var blatt = new Canvas { Width = 800, Height = 1100 };
            var klein = Rahmen(0, 0, 50, 12);
            var gross = Rahmen(0, 100, 50, 30);
            blatt.Children.Add(klein);
            blatt.Children.Add(gross);

            DossierPreviewLiveText.Zeige(blatt, [klein], "a");
            var kleineSchrift = ((TextBlock)blatt.Children.OfType<Border>()
                .Single(DossierPreviewLiveText.IstSofortanzeige).Child).FontSize;

            DossierPreviewLiveText.Zeige(blatt, [gross], "a");
            var grosseSchrift = ((TextBlock)blatt.Children.OfType<Border>()
                .Single(DossierPreviewLiveText.IstSofortanzeige).Child).FontSize;

            Assert.True(grosseSchrift > kleineSchrift);
        });
    }

    private static void RunOnSta(Action test)
    {
        ExceptionDispatchInfo? fehler = null;

        var thread = new Thread(() =>
        {
            try
            {
                test();
            }
            catch (Exception ex)
            {
                fehler = ExceptionDispatchInfo.Capture(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        fehler?.Throw();
    }
}
