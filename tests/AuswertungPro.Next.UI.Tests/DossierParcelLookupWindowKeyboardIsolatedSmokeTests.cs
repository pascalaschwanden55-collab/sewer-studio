using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.UI.Views.Windows;

using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Fix-Runde 1 zu Aufgabe 3 (Optikanalyse 28.09.2026): «Daten holen» verlor seinen
/// <c>IsDefault</c>, weil das Fenster jetzt genau EINEN Hauptknopf haben darf («Übernehmen»
/// im Fuß, gesperrt bis zu einem Fund). Ohne Ersatz löste Enter in Gemeinde/Parzelle dadurch
/// gar nichts mehr aus. <see cref="DossierParcelLookupWindow.OnLookupInputKeyDown"/> fängt
/// Enter jetzt NUR in diesen beiden Feldern ab und ruft denselben Weg wie der Knopf.
///
/// Dieser Test beweist es END-ZU-ENDE: ein echtes, im Fenster registriertes
/// <c>KeyDown</c>-Ereignis (Muster wie <see cref="ListenReihenfolgeIsolatedTests"/>, dort
/// bereits erfolgreich an einem <c>TextBox</c>-Enter geprüft) löst tatsächlich
/// <c>OnLookup</c> aus — sichtbar an der Validierungsmeldung, die nur dieser Weg setzt. Die
/// Gemeindeliste bleibt in diesem Test bewusst leer (Fake liefert nichts), damit «Bitte
/// zuerst eine Gemeinde wählen.» früh genug greift, ohne echte Netzabfragen zu benötigen.
/// </summary>
[Trait(TestKategorie.Name, TestKategorie.Kindprozess)]
[Collection("IsolatedWpf")]
public sealed class DossierParcelLookupWindowKeyboardIsolatedSmokeTests
{
    [Fact]
    public async Task Enter_in_Parzelle_loest_die_Abfrage_aus_im_eigenen_Wpf_Prozess()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(
            typeof(DossierParcelLookupWindowKeyboardIsolatedSmokeTests).FullName + "." + nameof(Kindprozess),
            TimeSpan.FromSeconds(60));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0 && result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            var parcels = new FakeParcelLookup();
            var lookup = new DossierParcelLookupUseCase(
                parcels, new FakeLandRegistryLookup(), new FakeSewerNetworkLookup());
            var directory = new FakeDirectoryLookup();

            var ctor = typeof(DossierParcelLookupWindow)
                .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single();
            var fenster = (DossierParcelLookupWindow)ctor.Invoke(new object[]
            {
                parcels,
                lookup,
                directory,
                (IReadOnlyDictionary<string, Guid>)new Dictionary<string, Guid>(),
                (IReadOnlyList<string>)Array.Empty<string>()
            });

            // Bewusst OHNE Show()/Activate(): das Fenster-Loaded stiesse sonst das async
            // Gemeinden-Laden an, und diese Testklasse braucht dafuer keinen echten
            // Nachrichten-Pumpvorgang - InitializeComponent() (in der Reflection-Erzeugung
            // ausgeloest) hat den Namescope und die KeyDown-Verdrahtung schon vollstaendig
            // aufgebaut, RaiseEvent erreicht registrierte Handler unabhaengig von Show().
            var parcelBox = (TextBox)fenster.FindName("ParcelBox");
            var municipalityBox = (ComboBox)fenster.FindName("MunicipalityBox");
            var statusText = (TextBlock)fenster.FindName("StatusText");

            Assert.Null(municipalityBox.SelectedItem);
            Assert.True(string.IsNullOrEmpty(statusText.Text), "StatusText sollte vor jeder Abfrage leer sein.");

            using var source = new HwndSource(new HwndSourceParameters("DossierParcelLookup-Enter-Test") { Width = 1, Height = 1 });

            // ── 1) Enter in der Parzellennummer loest OnLookup aus (sichtbar an der
            //      Validierungsmeldung "Bitte zuerst eine Gemeinde waehlen.", die NUR OnLookup
            //      setzt) und markiert das Ereignis als behandelt. ──
            parcelBox.Text = "1234";
            var parcelEnter = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent };
            parcelBox.RaiseEvent(parcelEnter);

            // OnLookup kehrt hier synchron zurueck (die Validierung liegt VOR dem ersten
            // await), deshalb steht das Ergebnis sofort fest - kein Dispatcher-Pump noetig.
            Assert.Equal("Bitte zuerst eine Gemeinde wählen.", statusText.Text);
            Assert.True(parcelEnter.Handled, "Enter im Parzellenfeld muss behandelt sein, damit es nicht zusaetzlich den eingebauten Standardknopf-Mechanismus erreicht.");

            // ── 2) Dasselbe fuer die Gemeinde-Combobox: leere Parzelle -> die andere
            //      Validierungsmeldung, ebenfalls nur ueber OnLookup erreichbar. ──
            statusText.Text = string.Empty;
            parcelBox.Text = string.Empty;
            var gemeindeEnter = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent };
            municipalityBox.RaiseEvent(gemeindeEnter);

            Assert.Equal("Bitte zuerst eine Gemeinde wählen.", statusText.Text);
            Assert.True(gemeindeEnter.Handled);

            // ── 3) Eine andere Taste in denselben Feldern loest nichts aus (kein Abfangen
            //      ausserhalb von Enter). ──
            statusText.Text = string.Empty;
            var anderesFeldTaste = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Tab) { RoutedEvent = Keyboard.KeyDownEvent };
            parcelBox.RaiseEvent(anderesFeldTaste);
            Assert.False(anderesFeldTaste.Handled);
            Assert.True(string.IsNullOrEmpty(statusText.Text));

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
            app.Shutdown();
        });
    }

    /// <summary>
    /// Textbasierter Umfangs-Waechter (kein WPF): der Enter-Abfang steht wirklich nur an
    /// <c>MunicipalityBox</c> und <c>ParcelBox</c> - nirgends sonst im Fenster, sonst wuerde
    /// z. B. Enter im Ergebnisbereich ebenfalls verschluckt.
    /// </summary>
    [Fact]
    public void Der_Enter_Abfang_steht_nur_an_Gemeinde_und_Parzelle()
    {
        var xaml = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Windows", "DossierParcelLookupWindow.xaml"));

        var gesamt = Regex.Matches(xaml, "KeyDown=\"OnLookupInputKeyDown\"").Count;
        Assert.Equal(2, gesamt);

        Assert.Matches(
            new Regex(@"<ComboBox\b[^>]*?x:Name=""MunicipalityBox""[^>]*?KeyDown=""OnLookupInputKeyDown""[^>]*?/>", RegexOptions.Singleline),
            xaml);
        Assert.Matches(
            new Regex(@"<TextBox\b[^>]*?x:Name=""ParcelBox""[^>]*?KeyDown=""OnLookupInputKeyDown""[^>]*?/>", RegexOptions.Singleline),
            xaml);
    }

    private sealed class FakeParcelLookup : IParcelLookup
    {
        public Task<ParcelInfo?> FindAsync(int bfsNr, string parcelNumber, CancellationToken ct = default)
            => throw new NotSupportedException("In diesem Testpfad nicht erwartet - die Validierung greift vorher.");

        public Task<IReadOnlyList<ParcelInfo>> FindTouchedAsync(IReadOnlyList<string> wktLines, CancellationToken ct = default)
            => throw new NotSupportedException("In diesem Testpfad nicht erwartet.");

        public Task<IReadOnlyList<Municipality>> ListMunicipalitiesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Municipality>>(Array.Empty<Municipality>());
    }

    private sealed class FakeLandRegistryLookup : ILandRegistryLookup
    {
        public Task<LandRegistryEntry?> ReadAsync(ParcelInfo parcel, CancellationToken ct = default)
            => throw new NotSupportedException("In diesem Testpfad nicht erwartet.");
    }

    private sealed class FakeSewerNetworkLookup : ISewerNetworkLookup
    {
        public Task<IReadOnlyList<NetworkHolding>> FindByNamesAsync(IReadOnlyList<string> names, CancellationToken ct = default)
            => throw new NotSupportedException("In diesem Testpfad nicht erwartet.");

        public Task<IReadOnlyList<NetworkHolding>> FindOnParcelAsync(ParcelInfo parcel, CancellationToken ct = default)
            => throw new NotSupportedException("In diesem Testpfad nicht erwartet.");
    }

    private sealed class FakeDirectoryLookup : IDirectoryLookup
    {
        public bool IsConfigured => false;
        public string Attribution => string.Empty;

        public Task<DirectoryLookupResult> FindAsync(string name, string town, CancellationToken ct = default)
            => throw new NotSupportedException("In diesem Testpfad nicht erwartet.");
    }
}
