using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Pages;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageContextMenuRecordResolverTests
{
    [Fact]
    public void ResolveFromSender_returns_record_from_framework_element_data_context()
    {
        RunOnSta(() =>
        {
            var record = new HaltungRecord();
            var item = new MenuItem { DataContext = record };

            Assert.Same(record, DataPageContextMenuRecordResolver.ResolveFromSender(item));
        });
    }

    [Fact]
    public void ResolveFromSender_returns_record_from_context_menu_row_placement_target()
    {
        RunOnSta(() =>
        {
            var record = new HaltungRecord();
            var menu = new ContextMenu
            {
                PlacementTarget = new DataGridRow { Item = record }
            };

            Assert.Same(record, DataPageContextMenuRecordResolver.ResolveFromSender(menu));
        });
    }

    [Fact]
    public void ResolveFromSender_returns_record_from_context_menu_grid_placement_target()
    {
        RunOnSta(() =>
        {
            var record = new HaltungRecord();
            var grid = new DataGrid { ItemsSource = new[] { record }, SelectedItem = record };
            var menu = new ContextMenu { PlacementTarget = grid };

            Assert.Same(record, DataPageContextMenuRecordResolver.ResolveFromSender(menu));
        });
    }

    /// <summary>
    /// Nova, Aufklapp-Liste: Dasselbe Zeilenmenue haengt jetzt auch an der Liste. Deren
    /// PlacementTarget ist eine ListBox — kein DataGrid und keine DataGridRow. Der Resolver
    /// findet dort nichts und muss auf die gewaehlte Haltung zurueckfallen; genau die hat der
    /// Rechtsklick vorher gesetzt. Wichtig ist, dass er NICHT die falsche Zeile liefert.
    /// </summary>
    [Fact]
    public void Resolve_nimmt_bei_einer_Liste_die_gewaehlte_Haltung()
    {
        RunOnSta(() =>
        {
            var angeklickt = new HaltungRecord();
            var andere = new HaltungRecord();
            var liste = new ListBox { ItemsSource = new[] { andere, angeklickt }, SelectedItem = angeklickt };
            var menu = new ContextMenu { PlacementTarget = liste };
            var punkt = new MenuItem();
            menu.Items.Add(punkt);

            Assert.Null(DataPageContextMenuRecordResolver.ResolveFromSender(punkt));
            Assert.Same(angeklickt, DataPageContextMenuRecordResolver.Resolve(punkt, angeklickt));
        });
    }

    [Fact]
    public void Resolve_falls_back_to_selected_record()
    {
        RunOnSta(() =>
        {
            var selected = new HaltungRecord();

            Assert.Same(selected, DataPageContextMenuRecordResolver.Resolve(new object(), selected));
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
