using System;
using System.IO;
using System.Linq;
using Expression = System.Linq.Expressions.Expression;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

internal static class Program
{
    [STAThread]
    static int Main()
    {
        var profile = Path.GetFullPath("C:/Sewer-Studio_KI_4.5/.tmp/nova-wpf-review/profile");
        Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", profile);
        var bin = "C:/Sewer-Studio_KI_4.5-nova/src/AuswertungPro.Next.UI/bin/Debug/net10.0-windows10.0.19041";
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var path = Path.Combine(bin, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        try
        {
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bin, "SewerStudio.dll"));
            var appType = assembly.GetType("AuswertungPro.Next.UI.App", true)!;
            var app = (System.Windows.Application)Activator.CreateInstance(appType)!;
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            appType.GetMethod("InitializeComponent")!.Invoke(app, null);
            // Kein Application.Run(), kein OnStartup, kein ServiceProvider und kein Projektladen.
            var pageType = assembly.GetType("AuswertungPro.Next.UI.Views.Pages.DataPage", true)!;
            var page = (UserControl)Activator.CreateInstance(pageType)!;
            page.Measure(new Size(1100, 650));
            page.Arrange(new Rect(0, 0, 1100, 650));
            page.UpdateLayout();
            Console.WriteLine(JsonSerializer.Serialize(new { result="constructed", width=page.ActualWidth, height=page.ActualHeight, applicationStartupInvoked=false, projectLoaded=false }));
            var domain = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bin, "AuswertungPro.Next.Domain.dll"));
            var recordType = domain.GetType("AuswertungPro.Next.Domain.Models.HaltungRecord", true)!;
            var sourceType = domain.GetType("AuswertungPro.Next.Domain.Models.FieldSource", true)!;
            var manual = Enum.Parse(sourceType, "Manual");
            var setField = recordType.GetMethod("SetFieldValue")!;
            var getField = recordType.GetMethod("GetFieldValue")!;
            var record = Activator.CreateInstance(recordType)!;
            setField.Invoke(record, new object[] { "Bemerkungen", "Alt", manual, true });
            var factoryType = assembly.GetType("AuswertungPro.Next.UI.DataPage.DataPageDetailItemFactory", true)!;
            var ctor = factoryType.GetConstructors().Single();
            var parameters = ctor.GetParameters();
            var field = Expression.Parameter(typeof(string), "field");
            var noCombo = Expression.Lambda(parameters[0].ParameterType,
                Expression.Constant(null, parameters[0].ParameterType.GenericTypeArguments[1]), field).Compile();
            var commit = pageType.GetMethod("CommitHaltungDetailField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .CreateDelegate(parameters[1].ParameterType, page);
            var factory = ctor.Invoke(new object?[] { noCombo, commit, null, null });
            var item = factoryType.GetMethod("Create")!.Invoke(factory, new object[] { "Bemerkungen", record })!;
            var itemValue = item.GetType().GetProperty("Value")!;
            setField.Invoke(record, new object[] { "Bemerkungen", "Neue Tabellenkorrektur", manual, true });
            var shownAfterChange = (string)itemValue.GetValue(item)!;
            var actualAfterChange = (string)getField.Invoke(record, new object[] { "Bemerkungen" })!;
            itemValue.SetValue(item, shownAfterChange + " + Zusatz im Formular");
            var finalValue = (string)getField.Invoke(record, new object[] { "Bemerkungen" })!;
            Console.WriteLine(JsonSerializer.Serialize(new { check="W01_FormularSnapshot", shownAfterChange, actualAfterChange, finalValue,
                staleValueReproduced=shownAfterChange=="Alt", newerValueOverwritten=!finalValue.Contains("Neue Tabellenkorrektur"),
                scope="Produktfactory, Datensatz und produktiver CommitHaltungDetailField; ohne ViewModel, Autosave oder vollstaendige Oberflaechenbedienung", projectLoaded=false }));
            var drawer = (FrameworkElement)page.FindName("FelderDrawer");
            var toggle = (System.Windows.Controls.Primitives.ToggleButton)drawer.FindName("OpenToggle");
            var drawerRow = (RowDefinition)page.FindName("DrawerRow");
            var heightBefore = drawerRow.ActualHeight;
            toggle.IsChecked = false;
            page.Measure(new Size(1100, 650));
            page.Arrange(new Rect(0, 0, 1100, 650));
            page.UpdateLayout();
            Console.WriteLine(JsonSerializer.Serialize(new { check="W03_FormularZuklappen", heightBefore, heightAfter=drawerRow.ActualHeight,
                checkedAfter=toggle.IsChecked, scope="Layout ohne ViewModel und ohne Fensterstart" }));
            return 0;
        }
        catch (Exception ex)
        {
            var list = new System.Collections.Generic.List<object>();
            for (Exception? e = ex; e != null; e = e.InnerException)
                list.Add(new { type=e.GetType().FullName, e.Message, e.StackTrace });
            Console.WriteLine(JsonSerializer.Serialize(new { result="failed", exceptions=list, applicationStartupInvoked=false, projectLoaded=false }));
            return 1;
        }
    }
}
