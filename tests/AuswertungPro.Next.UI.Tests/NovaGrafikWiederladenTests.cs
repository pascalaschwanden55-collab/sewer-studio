using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;

namespace AuswertungPro.Next.UI.Tests;

public sealed class NovaGrafikWiederladenTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Nach_Wiederladen_zeichnet_die_Grafik_Protokollaenderungen_selbst(bool schacht)
    {
        StaTestRunner.Run(() =>
        {
            var h = new HaltungRecord();
            h.SetFieldValue("Haltungsname", "A-B", FieldSource.Manual, false);
            h.SetFieldValue("Haltungslaenge_m", "20", FieldSource.Manual, false);
            var s = new SchachtRecord();
            s.SetFieldValue("Schachtnummer", "S1", FieldSource.Manual, false);
            UserControl grafik = schacht ? new SchachtgrafikControl { Record = s } : new HaltungsgrafikControl { Record = h };
            int Anzahl() => grafik is SchachtgrafikControl sg ? sg.SymbolAnzahl : ((HaltungsgrafikControl)grafik).SymbolAnzahl;
            var fenster = new Window { Content = grafik, Width = 350, Height = 650,
                Left = -10000, Top = -10000, ShowInTaskbar = false };
            foreach (var teil in new[] { "Theme/ThemeLight.xaml", "Theme/Controls.xaml", "Controls/NovaPageHeader.xaml" })
                fenster.Resources.MergedDictionaries.Add(new ResourceDictionary {
                    Source = new Uri($"pack://application:,,,/SewerStudio;component/{teil}") });
            try
            {
                fenster.Show(); Ruhen();
                Assert.Equal(0, Anzahl());
                fenster.Content = null; Ruhen();
                fenster.Content = grafik; Ruhen();
                var protokoll = new ProtocolDocument { Current = new ProtocolRevision {
                    Entries = { new ProtocolEntry { Code = "BAB", Beschreibung = "Riss", MeterStart = 2 } } } };
                if (schacht) s.Protocol = protokoll; else h.Protocol = protokoll;
                Ruhen();
                Assert.Equal(1, Anzahl());
            }
            finally { fenster.Close(); Ruhen(); }
        });
    }

    private static void Ruhen()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
