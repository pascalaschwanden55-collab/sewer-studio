using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Behaviors;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// F11-Regression: Befund-Kacheln duerfen nur innerhalb derselben Haltung/Session
/// (desselben PlayerWindow) gezogen werden. Drops aus fremden Fenstern werden verworfen
/// (Effects = None), Same-Haltungs-Drops laufen unveraendert weiter.
/// </summary>
public sealed class CodingEventDragDropSessionGuardTests
{
    [Fact]
    public void ResolveDropEffects_akzeptiert_same_session_drop_als_kopie_in_ki_spalte()
    {
        RunOnStaThread(() =>
        {
            var sessionKey = new object();
            var source = List(sessionKey, isKiColumn: false);
            var target = List(sessionKey, isKiColumn: true);
            var payload = Payload(source, sessionKey);

            var effects = CodingEventDragDropBehavior.ResolveDropEffects(payload, target);

            Assert.Equal(DragDropEffects.Copy, effects);
        });
    }

    [Fact]
    public void ResolveDropEffects_akzeptiert_same_session_drop_als_move_in_import_spalte()
    {
        RunOnStaThread(() =>
        {
            var sessionKey = new object();
            var source = List(sessionKey, isKiColumn: true);
            var target = List(sessionKey, isKiColumn: false);
            var payload = Payload(source, sessionKey);

            var effects = CodingEventDragDropBehavior.ResolveDropEffects(payload, target);

            Assert.Equal(DragDropEffects.Move, effects);
        });
    }

    [Fact]
    public void ResolveDropEffects_verwirft_drop_aus_fremder_session()
    {
        RunOnStaThread(() =>
        {
            // Zwei PlayerWindow-Instanzen = zwei verschiedene Session-Keys.
            var source = List(new object(), isKiColumn: true);
            var target = List(new object(), isKiColumn: false);
            var payload = Payload(source, CodingEventDragDropBehavior.GetSessionKey(source));

            var effects = CodingEventDragDropBehavior.ResolveDropEffects(payload, target);

            Assert.Equal(DragDropEffects.None, effects);
        });
    }

    [Fact]
    public void ResolveDropEffects_verwirft_drop_ohne_session_key()
    {
        RunOnStaThread(() =>
        {
            var source = List(sessionKey: null, isKiColumn: true);
            var target = List(sessionKey: null, isKiColumn: false);
            var payload = Payload(source, sourceSessionKey: null);

            var effects = CodingEventDragDropBehavior.ResolveDropEffects(payload, target);

            Assert.Equal(DragDropEffects.None, effects);
        });
    }

    [Fact]
    public void ResolveDropEffects_verwirft_drop_in_dieselbe_spalte()
    {
        RunOnStaThread(() =>
        {
            var sessionKey = new object();
            var source = List(sessionKey, isKiColumn: true);
            var payload = Payload(source, sessionKey);

            var effects = CodingEventDragDropBehavior.ResolveDropEffects(payload, source);

            Assert.Equal(DragDropEffects.None, effects);
        });
    }

    [Fact]
    public void ResolveDropEffects_verwirft_leeren_payload()
    {
        RunOnStaThread(() =>
        {
            var target = List(new object(), isKiColumn: true);

            Assert.Equal(
                DragDropEffects.None,
                CodingEventDragDropBehavior.ResolveDropEffects(null, target));
        });
    }

    [Fact]
    public void IsSameSession_nur_bei_gesetzten_gleichen_keys()
    {
        var key = new object();

        Assert.True(CodingEventDragDropBehavior.IsSameSession(key, key));
        Assert.False(CodingEventDragDropBehavior.IsSameSession(key, new object()));
        Assert.False(CodingEventDragDropBehavior.IsSameSession(null, key));
        Assert.False(CodingEventDragDropBehavior.IsSameSession(key, null));
        Assert.False(CodingEventDragDropBehavior.IsSameSession(null, null));
    }

    [Fact]
    public void Drag_payload_reist_mit_event_quellspalte_und_session_key_durchs_dataobject()
    {
        RunOnStaThread(() =>
        {
            var sessionKey = new object();
            var source = List(sessionKey, isKiColumn: true);
            var codingEvent = new CodingEvent { Entry = new ProtocolEntry { Code = "BBA" } };
            var payload = new CodingEventDragPayload(codingEvent, source, sessionKey);

            var data = CodingEventDragDropBehavior.CreateDragData(payload);
            var read = CodingEventDragDropBehavior.TryReadPayload(data);

            Assert.NotNull(read);
            Assert.Same(codingEvent, read.Event);
            Assert.Same(source, read.SourceList);
            Assert.Same(sessionKey, read.SourceSessionKey);
            Assert.False(read.ForeignHintShown);
            read.ForeignHintShown = true;
            Assert.True(read.ForeignHintShown);
        });
    }

    [Fact]
    public void TryReadPayload_liefert_null_bei_fremdem_format()
    {
        RunOnStaThread(() =>
        {
            var data = new DataObject("irgendwas.anderes", new object());

            Assert.Null(CodingEventDragDropBehavior.TryReadPayload(data));
            Assert.Null(CodingEventDragDropBehavior.TryReadPayload(null));
        });
    }

    private static ListBox List(object? sessionKey, bool isKiColumn)
    {
        var list = new ListBox();
        CodingEventDragDropBehavior.SetSessionKey(list, sessionKey);
        CodingEventDragDropBehavior.SetIsKiColumn(list, isKiColumn);
        return list;
    }

    private static CodingEventDragPayload Payload(ListBox source, object? sourceSessionKey)
        => new(
            new CodingEvent { Entry = new ProtocolEntry { Code = "BBA" } },
            source,
            sourceSessionKey);

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { exception = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
